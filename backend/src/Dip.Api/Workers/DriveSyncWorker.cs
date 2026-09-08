using Dip.Api.Hubs;
using Dip.Infrastructure.Drive;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dip.Api.Workers;

// Polls Google Drive every GoogleDrive:PollHours and whenever someone presses
// "Sync now" (decision D4). A trigger that arrives mid-run sets a run-again flag
// instead of starting a second concurrent walk.
public sealed class DriveSyncWorker : BackgroundService
{
    private readonly SyncTrigger _trigger;
    private readonly WorkerState _state;
    private readonly IServiceScopeFactory _scopes;
    private readonly GoogleDriveOptions _options;
    private readonly ILogger<DriveSyncWorker> _logger;

    public DriveSyncWorker(
        SyncTrigger trigger,
        WorkerState state,
        IServiceScopeFactory scopes,
        IOptions<GoogleDriveOptions> options,
        ILogger<DriveSyncWorker> logger)
    {
        _trigger = trigger;
        _state = state;
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.RootFolderId))
        {
            _logger.LogInformation(
                "GoogleDrive:RootFolderId is not set — Drive polling is disabled; uploads still import");
            _state.PollingEnabled = false;
            return;
        }
        _state.PollingEnabled = true;

        var timerTask = _options.PollHours > 0
            ? RunTimerAsync(stoppingToken)
            : Task.CompletedTask;

        await RunTriggersAsync(stoppingToken);
        await timerTask;
    }

    private async Task RunTimerAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), ct);
            var period = TimeSpan.FromHours(_options.PollHours);
            _state.NextRun(DateTime.UtcNow);

            using var timer = new PeriodicTimer(period);
            while (!ct.IsCancellationRequested)
            {
                await SyncAllProjectsAsync(ct);
                _state.NextRun(DateTime.UtcNow.Add(period));
                if (!await timer.WaitForNextTickAsync(ct)) return;
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutting down.
        }
    }

    private async Task RunTriggersAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Guid projectId;
            try
            {
                projectId = await _trigger.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await SyncProjectAsync(projectId, ct);
        }
    }

    private async Task SyncAllProjectsAsync(CancellationToken ct)
    {
        List<Guid> projects;
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            projects = await db.Projects.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not list projects for the scheduled Drive sync");
            return;
        }

        foreach (var projectId in projects)
        {
            await SyncProjectAsync(projectId, ct);
        }
    }

    private async Task SyncProjectAsync(Guid projectId, CancellationToken ct)
    {
        if (!_state.TryStartSync(DateTime.UtcNow))
        {
            // A walk is in flight; it re-runs once it finishes rather than racing it.
            _state.RequestRunAgain();
            return;
        }

        do
        {
            var error = await RunOnceAsync(projectId, ct);
            _state.SyncFinished(DateTime.UtcNow, error);
        }
        while (!ct.IsCancellationRequested && _state.ConsumeRunAgain() && _state.TryStartSync(DateTime.UtcNow));
    }

    private async Task<string?> RunOnceAsync(Guid projectId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var sync = scope.ServiceProvider.GetRequiredService<DriveSyncService>();
            var result = await sync.SyncProjectAsync(projectId, ct);
            return result.Error;
        }
        catch (OperationCanceledException)
        {
            return "cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drive sync failed for project {ProjectId}", projectId);
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ISyncNotifier>()
                    .SyncFinishedAsync(projectId, 0, 0, ex.Message);
            }
            catch (Exception notifyError)
            {
                _logger.LogWarning(notifyError, "Could not publish the sync failure");
            }
            return ex.Message;
        }
    }
}
