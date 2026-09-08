using Dip.Api.Features.Imports;

namespace Dip.Api.Hubs;

// Services raise progress through this port so nothing outside Hubs/ depends on
// SignalR types — and so the integration tests can drop a no-op in its place.
public interface ISyncNotifier
{
    Task SyncStartedAsync(Guid projectId, DateTime at);
    Task FolderSyncedAsync(Guid projectId, Guid folderId, string path, int files);
    Task SyncFinishedAsync(Guid projectId, int foldersSynced, int filesQueued, string? error);
    Task FileQueuedAsync(Guid projectId, Guid fileId, Guid folderId);
    Task FileImportStartedAsync(Guid projectId, Guid fileId);
    Task FileImportedAsync(Guid projectId, Guid fileId, Guid folderId, ImportBatchSummary batch);
    Task FileFailedAsync(Guid projectId, Guid fileId, Guid folderId, string error);
    Task RecalculationFinishedAsync(Guid projectId, int documents);
}
