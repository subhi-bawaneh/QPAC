using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.DownloadFile;

[Route("api/folder-files/{id:guid}/download")]
[Authorize]
public sealed class DownloadFileController : ApiControllerBase
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var file = await Dispatcher.Query(new DownloadFileQuery(id), ct);
        return File(file.Content, XlsxContentType, file.Name);
    }
}
