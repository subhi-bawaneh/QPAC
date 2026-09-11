using Dip.Api.Features.Imports;
using Microsoft.AspNetCore.SignalR;

namespace Dip.Api.Hubs;

public sealed class HubSyncNotifier : ISyncNotifier
{
    private readonly IHubContext<SyncHub> _hub;

    public HubSyncNotifier(IHubContext<SyncHub> hub) => _hub = hub;

    private IClientProxy Group(Guid projectId) => _hub.Clients.Group(SyncHub.GroupFor(projectId));

    public Task ImportQueuedAsync(Guid projectId, Guid batchId, string fileName) =>
        Group(projectId).SendAsync("importQueued", new { projectId, batchId, fileName });

    public Task ImportStartedAsync(Guid projectId, Guid batchId) =>
        Group(projectId).SendAsync("importStarted", new { projectId, batchId });

    public Task ImportFinishedAsync(Guid projectId, Guid batchId, ImportBatchSummary batch) =>
        Group(projectId).SendAsync("importFinished", new { projectId, batchId, batch });

    public Task ImportFailedAsync(Guid projectId, Guid batchId, string error) =>
        Group(projectId).SendAsync("importFailed", new { projectId, batchId, error });

    public Task RecalculationFinishedAsync(Guid projectId, int documents) =>
        Group(projectId).SendAsync("recalculationFinished", new { projectId, documents });
}
