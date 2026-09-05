using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Summaries.GetBaselineSummary;

[Route("api/projects/{projectId:guid}/summaries/baseline")]
[Authorize]
public sealed class GetBaselineSummaryController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(BaselineSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaselineSummaryResponse>> Get(Guid projectId, CancellationToken ct)
    {
        var result = await Dispatcher.Query(new GetBaselineSummaryQuery(projectId), ct);
        return Ok(result);
    }
}
