using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.GetDriveStatus;

[Route("api/projects/{projectId:guid}/drive/status")]
[Authorize]
public sealed class GetDriveStatusController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(DriveStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DriveStatusDto>> Get(Guid projectId, CancellationToken ct) =>
        Ok(await Dispatcher.Query(new GetDriveStatusQuery(projectId), ct));
}
