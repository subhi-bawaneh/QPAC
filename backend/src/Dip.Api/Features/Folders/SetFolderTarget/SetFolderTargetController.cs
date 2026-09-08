using Dip.Api.Common;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.SetFolderTarget;

[Route("api/folders/{id:guid}/target")]
[Authorize]
public sealed class SetFolderTargetController : ApiControllerBase
{
    public sealed record SetTargetRequest(DataTarget Target);

    [HttpPut]
    [ProducesResponseType(typeof(SetTargetResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SetTargetResult>> Put(
        Guid id, [FromBody] SetTargetRequest body, CancellationToken ct) =>
        Ok(await Dispatcher.Send(new SetFolderTargetCommand(id, body.Target), ct));
}
