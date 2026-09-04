using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.SyncFromDrive;

[Route("api/projects/{projectId:guid}/drive/sync")]
[Authorize]
public sealed class SyncFromDriveController : ApiControllerBase
{
    public sealed record SyncRequest(string? StartFolderDriveId, bool DownloadFiles);

    [HttpPost]
    [ProducesResponseType(typeof(SyncStepResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SyncStepResult>> Post(
        Guid projectId,
        [FromBody] SyncRequest body,
        CancellationToken ct)
    {
        var result = await Dispatcher.Send(
            new SyncFromDriveCommand(projectId, body.StartFolderDriveId, body.DownloadFiles),
            ct);
        return Ok(result);
    }
}
