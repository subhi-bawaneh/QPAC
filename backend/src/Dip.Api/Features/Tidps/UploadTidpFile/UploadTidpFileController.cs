using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Tidps.UploadTidpFile;

[Route("api/projects/{projectId:guid}/tidp-files")]
[Authorize]
public sealed class UploadTidpFileController : ApiControllerBase
{
    // 50 MB: the largest sample workbook is 11 MB, and a TIDP is a tenth of that.
    private const long MaxBytes = 50L * 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxBytes)]
    [ProducesResponseType(typeof(UploadAccepted), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UploadAccepted>> Post(
        Guid projectId, IFormFile file, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);

        var result = await Dispatcher.Send(
            new UploadTidpFileCommand(projectId, file.FileName, buffer.ToArray()), ct);

        return Accepted(result);
    }
}
