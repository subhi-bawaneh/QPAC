using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.ControlFindings.GetControlFindings;

[Route("api/projects/{projectId:guid}/control-findings")]
[Authorize]
public sealed class GetControlFindingsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ControlFindingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ControlFindingsResponse>> Get(Guid projectId, CancellationToken ct)
    {
        var result = await Dispatcher.Query(new GetControlFindingsQuery(projectId), ct);
        return Ok(result);
    }
}
