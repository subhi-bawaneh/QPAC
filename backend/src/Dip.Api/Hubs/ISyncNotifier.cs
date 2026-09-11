using Dip.Api.Features.Imports;

namespace Dip.Api.Hubs;

// Services raise progress through this port so nothing outside Hubs/ depends on
// SignalR types — and so the integration tests can drop a no-op in its place.
//
// The four Drive-sync events are gone with Drive. What is left is the life of one
// upload plus the recalculation that follows it, keyed by ImportBatchId.
public interface ISyncNotifier
{
    Task ImportQueuedAsync(Guid projectId, Guid batchId, string fileName);
    Task ImportStartedAsync(Guid projectId, Guid batchId);
    Task ImportFinishedAsync(Guid projectId, Guid batchId, ImportBatchSummary batch);
    Task ImportFailedAsync(Guid projectId, Guid batchId, string error);
    Task RecalculationFinishedAsync(Guid projectId, int documents);
}
