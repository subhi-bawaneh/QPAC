using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Lists.GetPicklists;

[Route("api/projects/{projectId:guid}/picklists")]
[Authorize]
public sealed class GetPicklistsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PicklistGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PicklistGroupDto>>> Get(
        Guid projectId, CancellationToken ct)
    {
        var result = await Dispatcher.Query(new GetPicklistsQuery(projectId), ct);
        return Ok(result);
    }
}
