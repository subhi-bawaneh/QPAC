using Dip.Api.Common;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Tracker.ListTrackerDocuments;

[Route("api/projects/{projectId:guid}/tracker")]
[Authorize]
public sealed class ListTrackerDocumentsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TrackerRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<TrackerRowDto>>> Get(
        Guid projectId,
        [FromQuery] string? discipline,
        [FromQuery] UnifiedStatus? status,
        [FromQuery] string? search,
        [FromQuery] bool? hasAconex,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await Dispatcher.Query(
            new ListTrackerDocumentsQuery(projectId, discipline, status, search, hasAconex, page, pageSize), ct);
        return Ok(result);
    }
}
