using Dip.Api.Features.Imports;

namespace Dip.Api.Hubs;

// Used when SignalR is not wired up (integration tests): every event is dropped.
public sealed class NoopSyncNotifier : ISyncNotifier
{
    public Task ImportQueuedAsync(Guid projectId, Guid batchId, string fileName) => Task.CompletedTask;
    public Task ImportStartedAsync(Guid projectId, Guid batchId) => Task.CompletedTask;
    public Task ImportFinishedAsync(Guid projectId, Guid batchId, ImportBatchSummary batch) => Task.CompletedTask;
    public Task ImportFailedAsync(Guid projectId, Guid batchId, string error) => Task.CompletedTask;
    public Task RecalculationFinishedAsync(Guid projectId, int documents) => Task.CompletedTask;
}
