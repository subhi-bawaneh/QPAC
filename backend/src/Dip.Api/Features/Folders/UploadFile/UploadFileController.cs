using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.UploadFile;

[Route("api/folders/{folderId:guid}/files")]
[Authorize]
public sealed class UploadFileController : ApiControllerBase
{
    // Multipart form data; single "file" part. Max upload size is enforced by
    // ASPMonster's IIS config (50 MB per PLAN.md § 1) and the app-wide
    // FormOptions.MultipartBodyLengthLimit set in AddApi().
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<Guid>> Post(Guid folderId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file supplied" });
        }

        await using var stream = file.OpenReadStream();
        var id = await Dispatcher.Send(
            new UploadFileCommand(folderId, file.FileName, file.Length, stream),
            ct);
        return CreatedAtAction("Get", "GetFolder", new { id = folderId }, id);
    }
}
