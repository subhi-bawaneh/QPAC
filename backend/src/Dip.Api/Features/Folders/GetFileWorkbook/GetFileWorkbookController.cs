using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.GetFileWorkbook;

[Route("api/folder-files/{id:guid}/workbook")]
[Authorize]
public sealed class GetFileWorkbookController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(WorkbookDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkbookDto>> Get(
        Guid id,
        [FromQuery] string? sheet,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 500,
        CancellationToken ct = default) =>
        Ok(await Dispatcher.Query(new GetFileWorkbookQuery(id, sheet, page, pageSize), ct));
}
