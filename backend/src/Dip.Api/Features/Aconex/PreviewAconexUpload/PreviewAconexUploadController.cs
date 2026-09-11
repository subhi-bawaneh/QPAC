using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Aconex.PreviewAconexUpload;

[Route("api/projects/{projectId:guid}/aconex/preview")]
[Authorize]
public sealed class PreviewAconexUploadController : ApiControllerBase
{
    private const long MaxBytes = 200L * 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxBytes)]
    [ProducesResponseType(typeof(AconexPreview), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AconexPreview>> Post(
        Guid projectId, [FromForm] IFormFileCollection files, CancellationToken ct = default)
    {
        var uploads = new List<UploadedFile>(files.Count);
        foreach (var file in files)
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            uploads.Add(new UploadedFile(file.FileName, buffer.ToArray()));
        }

        return Ok(await Dispatcher.Send(new PreviewAconexUploadCommand(projectId, uploads), ct));
    }
}
