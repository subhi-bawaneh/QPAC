using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Lists.GetStatusMappings;

[Route("api/projects/{projectId:guid}/status-mappings")]
[Authorize]
public sealed class GetStatusMappingsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StatusMappingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<StatusMappingDto>>> Get(
        Guid projectId, CancellationToken ct)
    {
        var result = await Dispatcher.Query(new GetStatusMappingsQuery(projectId), ct);
        return Ok(result);
    }
}
