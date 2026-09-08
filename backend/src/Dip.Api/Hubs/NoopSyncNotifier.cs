using Dip.Api.Features.Imports;

namespace Dip.Api.Hubs;

// Used by the integration tests, which drive the workers directly and assert on
// the database rather than on hub traffic.
public sealed class NoopSyncNotifier : ISyncNotifier
{
    public Task SyncStartedAsync(Guid projectId, DateTime at) => Task.CompletedTask;
    public Task FolderSyncedAsync(Guid projectId, Guid folderId, string path, int files) => Task.CompletedTask;
    public Task SyncFinishedAsync(Guid projectId, int foldersSynced, int filesQueued, string? error) => Task.CompletedTask;
    public Task FileQueuedAsync(Guid projectId, Guid fileId, Guid folderId) => Task.CompletedTask;
    public Task FileImportStartedAsync(Guid projectId, Guid fileId) => Task.CompletedTask;
    public Task FileImportedAsync(Guid projectId, Guid fileId, Guid folderId, ImportBatchSummary batch) => Task.CompletedTask;
    public Task FileFailedAsync(Guid projectId, Guid fileId, Guid folderId, string error) => Task.CompletedTask;
    public Task RecalculationFinishedAsync(Guid projectId, int documents) => Task.CompletedTask;
}
