using Dip.Api.Features.Recalculation;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Workers;

// Consumes WorkQueue one item at a time for the life of the process.
//
// On startup it sweeps rather than re-queues: an upload's bytes live only in the
// queue, so a batch left Queued or Running by a restart can never be finished and is
// marked Failed with a message telling the operator to upload again. Pretending it is
// still pending would leave a row that never completes and never explains itself.
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
            case WorkItemKind.ImportBatch:
                // Stage 3 wires ImportService in here; until then nothing enqueues one.
                _logger.LogWarning("No import service is registered for batch {Id}", item.Id);
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

            // An unfinished batch cannot be resumed: its bytes went with the queue.
            var swept = await db.ImportBatches
                .Where(b => b.Status == ImportBatchStatus.Queued || b.Status == ImportBatchStatus.Running)
                .ExecuteUpdateAsync(
                    u => u.SetProperty(b => b.Status, ImportBatchStatus.Failed)
                          .SetProperty(b => b.Log, "{\"warnings\":[],\"error\":\"interrupted, upload again\"}"),
                    ct);

            var sweptFiles = await db.TidpFiles
                .Where(t => t.Status == TidpFileStatus.Importing)
                .ExecuteUpdateAsync(
                    u => u.SetProperty(t => t.Status, TidpFileStatus.Failed)
                          .SetProperty(t => t.Error, "interrupted, upload again"),
                    ct);

            if (swept > 0 || sweptFiles > 0)
            {
                _logger.LogWarning(
                    "Swept {Batches} interrupted import batch(es) and {Files} TIDP file(s) to Failed",
                    swept, sweptFiles);
            }

            // Snapshots may be behind whatever changed while the process was down
            // (an interrupted run), and a recalculation
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
            _logger.LogWarning(ex, "Could not sweep interrupted imports at startup");
        }
    }
}
