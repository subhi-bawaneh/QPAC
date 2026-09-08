using Dip.Api.Features.Imports;
using Microsoft.AspNetCore.SignalR;

namespace Dip.Api.Hubs;

public sealed class HubSyncNotifier : ISyncNotifier
{
    private readonly IHubContext<SyncHub> _hub;

    public HubSyncNotifier(IHubContext<SyncHub> hub) => _hub = hub;

    private IClientProxy Group(Guid projectId) => _hub.Clients.Group(SyncHub.GroupFor(projectId));

    public Task SyncStartedAsync(Guid projectId, DateTime at) =>
        Group(projectId).SendAsync("syncStarted", new { projectId, at });

    public Task FolderSyncedAsync(Guid projectId, Guid folderId, string path, int files) =>
        Group(projectId).SendAsync("folderSynced", new { projectId, folderId, path, files });

    public Task SyncFinishedAsync(Guid projectId, int foldersSynced, int filesQueued, string? error) =>
        Group(projectId).SendAsync("syncFinished", new { projectId, foldersSynced, filesQueued, error });

    public Task FileQueuedAsync(Guid projectId, Guid fileId, Guid folderId) =>
        Group(projectId).SendAsync("fileQueued", new { projectId, fileId, folderId });

    public Task FileImportStartedAsync(Guid projectId, Guid fileId) =>
        Group(projectId).SendAsync("fileImportStarted", new { projectId, fileId });

    public Task FileImportedAsync(Guid projectId, Guid fileId, Guid folderId, ImportBatchSummary batch) =>
        Group(projectId).SendAsync("fileImported", new { projectId, fileId, folderId, batch });

    public Task FileFailedAsync(Guid projectId, Guid fileId, Guid folderId, string error) =>
        Group(projectId).SendAsync("fileFailed", new { projectId, fileId, folderId, error });

    public Task RecalculationFinishedAsync(Guid projectId, int documents) =>
        Group(projectId).SendAsync("recalculationFinished", new { projectId, documents });
}
