using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dip.Api.Hubs;

// Progress channel for the Drive sync and import workers. Clients join one group
// per project and receive the events declared on ISyncNotifier.
[Authorize]
public sealed class SyncHub : Hub
{
    public static string GroupFor(Guid projectId) => $"project:{projectId}";

    public Task JoinProject(Guid projectId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(projectId));

    public Task LeaveProject(Guid projectId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(projectId));
}
