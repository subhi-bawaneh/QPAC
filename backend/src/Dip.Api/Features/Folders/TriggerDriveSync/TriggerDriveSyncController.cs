using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.TriggerDriveSync;

[Route("api/projects/{projectId:guid}/drive/sync")]
[Authorize]
public sealed class TriggerDriveSyncController : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TriggerSyncResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TriggerSyncResult>> Post(Guid projectId, CancellationToken ct)
    {
        var result = await Dispatcher.Send(new TriggerDriveSyncCommand(projectId), ct);
        return Accepted(result);
    }
}
