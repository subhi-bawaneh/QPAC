using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Drafts.GetPromoteDiff;

[Route("api/drafts/promote-diff")]
[Authorize]
public sealed class GetPromoteDiffController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PromoteDiffDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromoteDiffDto>> Get(
        [FromQuery] Guid folderFileId,
        [FromQuery] int maxRows = 200,
        CancellationToken ct = default)
    {
        var result = await Dispatcher.Query(new GetPromoteDiffQuery(folderFileId, maxRows), ct);
        return Ok(result);
    }
}
