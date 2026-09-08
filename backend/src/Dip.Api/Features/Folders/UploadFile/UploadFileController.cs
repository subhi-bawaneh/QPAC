using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.UploadFile;

[Route("api/folders/{folderId:guid}/files")]
[Authorize]
public sealed class UploadFileController : ApiControllerBase
{
    // Multipart form data; single "file" part. 202 because the import itself runs
    // in the background worker and the caller follows it over the sync hub.
    [HttpPost]
    [ProducesResponseType(typeof(UploadResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestSizeLimit(UploadFileValidator.MaxBytes)]
    public async Task<ActionResult<UploadResult>> Post(Guid folderId, IFormFile file, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        if (file is not null)
        {
            await file.CopyToAsync(buffer, ct);
        }

        var result = await Dispatcher.Send(
            new UploadFileCommand(folderId, file?.FileName ?? string.Empty, buffer.ToArray()),
            ct);
        return Accepted(result);
    }
}
