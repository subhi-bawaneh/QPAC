using Dip.Api.Common;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Baseline.ListBaselineActivities;

[Route("api/projects/{projectId:guid}/baseline")]
[Authorize]
public sealed class ListBaselineActivitiesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BaselineActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<BaselineActivityDto>>> Get(
        Guid projectId,
        [FromQuery] BaselineActivityType? type,
        [FromQuery] bool? used,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await Dispatcher.Query(
            new ListBaselineActivitiesQuery(projectId, type, used, search, page, pageSize), ct);
        return Ok(result);
    }
}
