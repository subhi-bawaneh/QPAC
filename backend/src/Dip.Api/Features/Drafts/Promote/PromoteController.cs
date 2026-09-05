using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Drafts.Promote;

[Route("api/drafts/promote")]
[Authorize]
public sealed class PromoteController : ApiControllerBase
{
    public sealed record PromoteRequest(Guid FolderFileId, bool DeleteMissing = false);

    [HttpPost]
    [ProducesResponseType(typeof(PromoteResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromoteResultDto>> Post(
        [FromBody] PromoteRequest body, CancellationToken ct)
    {
        var result = await Dispatcher.Send(
            new PromoteCommand(body.FolderFileId, body.DeleteMissing), ct);
        return Ok(result);
    }
}
