using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Summaries.GetEvm;

[Route("api/projects/{projectId:guid}/summaries/evm")]
[Authorize]
public sealed class GetEvmController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(EvmResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvmResponse>> Get(
        Guid projectId, [FromQuery] DateTime? reportDate, CancellationToken ct)
    {
        var result = await Dispatcher.Query(new GetEvmQuery(projectId, reportDate), ct);
        return Ok(result);
    }
}
