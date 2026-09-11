using Dip.Api.Common;
using Dip.Api.Features.Tidps.UploadTidpFile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Tidps.ReplaceTidpFile;

[Route("api/tidp-files/{id:guid}")]
[Authorize]
public sealed class ReplaceTidpFileController : ApiControllerBase
{
    private const long MaxBytes = 50L * 1024 * 1024;

    [HttpPut]
    [RequestSizeLimit(MaxBytes)]
    [ProducesResponseType(typeof(UploadAccepted), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadAccepted>> Put(
        Guid id, IFormFile file, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);

        var result = await Dispatcher.Send(
            new ReplaceTidpFileCommand(id, file.FileName, buffer.ToArray()), ct);

        return Accepted(result);
    }
}
