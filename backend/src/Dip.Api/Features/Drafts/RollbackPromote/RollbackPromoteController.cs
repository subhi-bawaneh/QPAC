using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Drafts.RollbackPromote;

[Route("api/drafts/promote/{promoteBatchId:guid}/rollback")]
[Authorize]
public sealed class RollbackPromoteController : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(RollbackPromoteResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RollbackPromoteResultDto>> Post(
        Guid promoteBatchId, CancellationToken ct)
    {
        var result = await Dispatcher.Send(new RollbackPromoteCommand(promoteBatchId), ct);
        return Ok(result);
    }
}
