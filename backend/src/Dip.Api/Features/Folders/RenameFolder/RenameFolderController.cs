using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.RenameFolder;

[Route("api/folders/{id:guid}/name")]
[Authorize]
public sealed class RenameFolderController : ApiControllerBase
{
    public sealed record RenameRequest(string NewName);

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Put(Guid id, [FromBody] RenameRequest body, CancellationToken ct)
    {
        await Dispatcher.Send(new RenameFolderCommand(id, body.NewName), ct);
        return NoContent();
    }
}
