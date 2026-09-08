namespace Dip.Api.Workers;

// What GET /api/projects/{id}/drive/status reports. Written by DriveSyncWorker,
// read by the status query and the Drive health check.
public sealed class WorkerState
{
    private readonly WorkQueue _queue;
    private readonly object _gate = new();

    private bool _isSyncRunning;
    private DateTime? _lastRunStartedAt;
    private DateTime? _lastRunFinishedAt;
    private string? _lastRunError;
    private DateTime? _nextRunAt;

    public WorkerState(WorkQueue queue) => _queue = queue;

    public int QueuedImports => _queue.PendingCount;

    public bool IsSyncRunning { get { lock (_gate) { return _isSyncRunning; } } }
    public DateTime? LastRunStartedAt { get { lock (_gate) { return _lastRunStartedAt; } } }
    public DateTime? LastRunFinishedAt { get { lock (_gate) { return _lastRunFinishedAt; } } }
    public string? LastRunError { get { lock (_gate) { return _lastRunError; } } }
    public DateTime? NextRunAt { get { lock (_gate) { return _nextRunAt; } } }

    public void SyncStarted(DateTime at)
    {
        lock (_gate)
        {
            _isSyncRunning = true;
            _lastRunStartedAt = at;
            _lastRunError = null;
        }
    }

    public void SyncFinished(DateTime at, string? error)
    {
        lock (_gate)
        {
            _isSyncRunning = false;
            _lastRunFinishedAt = at;
            _lastRunError = error;
        }
    }

    public void NextRun(DateTime? at)
    {
        lock (_gate) { _nextRunAt = at; }
    }
}
