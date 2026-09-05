using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.DeleteFolder;

[Route("api/folders/{id:guid}")]
[Authorize]
public sealed class DeleteFolderController : ApiControllerBase
{
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Dispatcher.Send(new DeleteFolderCommand(id), ct);
        return NoContent();
    }
}
