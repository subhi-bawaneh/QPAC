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
    private bool _runAgain;

    // False when GoogleDrive:RootFolderId is empty: "Sync now" then has nothing to run.
    public bool PollingEnabled { get; set; }

    public WorkerState(WorkQueue queue) => _queue = queue;

    public int QueuedImports => _queue.PendingCount;

    public bool IsSyncRunning { get { lock (_gate) { return _isSyncRunning; } } }
    public DateTime? LastRunStartedAt { get { lock (_gate) { return _lastRunStartedAt; } } }
    public DateTime? LastRunFinishedAt { get { lock (_gate) { return _lastRunFinishedAt; } } }
    public string? LastRunError { get { lock (_gate) { return _lastRunError; } } }
    public DateTime? NextRunAt { get { lock (_gate) { return _nextRunAt; } } }

    // Atomic check-and-start: the timer loop and the trigger loop run concurrently,
    // and only one of them may walk the tree at a time.
    public bool TryStartSync(DateTime at)
    {
        lock (_gate)
        {
            if (_isSyncRunning) return false;
            _isSyncRunning = true;
            _lastRunStartedAt = at;
            _lastRunError = null;
            _runAgain = false;
            return true;
        }
    }

    // A "Sync now" that lands mid-run is honoured as soon as the run finishes.
    public bool RequestRunAgain()
    {
        lock (_gate)
        {
            if (!_isSyncRunning) return false;
            _runAgain = true;
            return true;
        }
    }

    public bool ConsumeRunAgain()
    {
        lock (_gate)
        {
            var again = _runAgain;
            _runAgain = false;
            return again;
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
