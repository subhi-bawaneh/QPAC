using Dip.Api.Features.Recalculation;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Workers;

// Consumes WorkQueue one item at a time for the life of the process. On startup it
// re-queues every file that was never imported (or went stale), so a restart in the
// middle of a Drive poll finishes the job rather than losing it.
public sealed class ImportWorker : BackgroundService
{
    private readonly WorkQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ImportWorker> _logger;

    public ImportWorker(WorkQueue queue, IServiceScopeFactory scopes, ILogger<ImportWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            WorkItem item;
            try
            {
                item = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await RunAsync(item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // One bad workbook must never take the worker down with it.
                _logger.LogError(ex, "Work item {Kind} {Id} failed", item.Kind, item.Id);
            }
        }
    }

    private async Task RunAsync(WorkItem item, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        switch (item.Kind)
        {
            case WorkItemKind.ImportFile:
                await scope.ServiceProvider.GetRequiredService<FileImportService>().ImportAsync(item.Id, ct);
                break;

            case WorkItemKind.Recalculate:
                // RunAllAsync publishes its own completion event.
                await scope.ServiceProvider.GetRequiredService<RecalculationService>()
                    .RunAllAsync(item.Id, ct);
                break;
        }
    }

    private async Task RequeuePendingAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            var pending = await db.FolderFiles
                .AsNoTracking()
                .Where(f => !f.IsDeleted
                    && (f.State == ImportState.NotImported || f.State == ImportState.Outdated))
                .Select(f => f.Id)
                .ToListAsync(ct);

            foreach (var id in pending)
            {
                _queue.EnqueueImport(id);
            }

            // Snapshots may be behind whatever changed while the process was down
            // (a target flipped by hand, an interrupted run), and a recalculation
            // over an already-current set is cheap, so every project gets one.
            var projects = await db.Projects.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
            foreach (var projectId in projects)
            {
                _queue.EnqueueRecalculate(projectId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A database that is not up yet must not crash the host.
            _logger.LogWarning(ex, "Could not re-queue pending imports at startup");
        }
    }
}
