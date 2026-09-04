using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.DeleteFile;

[Route("api/folder-files/{id:guid}")]
[Authorize]
public sealed class DeleteFileController : ApiControllerBase
{
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Dispatcher.Send(new DeleteFileCommand(id), ct);
        return NoContent();
    }
}
