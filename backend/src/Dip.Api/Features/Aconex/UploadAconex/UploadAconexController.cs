using Dip.Api.Common;
using Dip.Api.Features.Aconex.PreviewAconexUpload;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Aconex.UploadAconex;

[Route("api/projects/{projectId:guid}/aconex/upload")]
[Authorize]
public sealed class UploadAconexController : ApiControllerBase
{
    private const long MaxBytes = 200L * 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxBytes)]
    [ProducesResponseType(typeof(AconexUploadAccepted), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AconexUploadAccepted>> Post(
        Guid projectId, [FromForm] IFormFileCollection files, CancellationToken ct = default)
    {
        var uploads = new List<UploadedFile>(files.Count);
        foreach (var file in files)
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            uploads.Add(new UploadedFile(file.FileName, buffer.ToArray()));
        }

        return Accepted(await Dispatcher.Send(new UploadAconexCommand(projectId, uploads), ct));
    }
}
