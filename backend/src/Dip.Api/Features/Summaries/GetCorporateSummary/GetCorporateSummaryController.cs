using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Summaries.GetCorporateSummary;

[Route("api/projects/{projectId:guid}/summaries/corporate")]
[Authorize]
public sealed class GetCorporateSummaryController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CorporateSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CorporateSummaryResponse>> Get(
        Guid projectId, [FromQuery] DateTime? reportDate, CancellationToken ct)
    {
        var result = await Dispatcher.Query(new GetCorporateSummaryQuery(projectId, reportDate), ct);
        return Ok(result);
    }
}
