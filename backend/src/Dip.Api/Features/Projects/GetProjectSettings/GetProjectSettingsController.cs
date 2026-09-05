using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Projects.GetProjectSettings;

[Route("api/projects/{projectId:guid}/settings")]
[Authorize]
public sealed class GetProjectSettingsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ProjectSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectSettingsDto>> Get(Guid projectId, CancellationToken ct)
    {
        var result = await Dispatcher.Query(new GetProjectSettingsQuery(projectId), ct);
        return Ok(result);
    }
}
