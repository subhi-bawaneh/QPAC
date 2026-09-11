using Dip.Api.Common;
using Dip.Api.Features.Tidps.UploadTidpFile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Baseline.UploadBaseline;

[Route("api/projects/{projectId:guid}/baseline/upload")]
[Authorize]
public sealed class UploadBaselineController : ApiControllerBase
{
    private const long MaxBytes = 50L * 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxBytes)]
    [ProducesResponseType(typeof(UploadAccepted), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UploadAccepted>> Post(
        Guid projectId, IFormFile file, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        return Accepted(await Dispatcher.Send(
            new UploadBaselineCommand(projectId, file.FileName, buffer.ToArray()), ct));
    }
}
