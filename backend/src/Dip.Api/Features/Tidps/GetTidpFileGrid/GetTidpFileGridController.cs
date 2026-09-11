using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Tidps.GetTidpFileGrid;

[Route("api/tidp-files/{id:guid}/grid")]
[Authorize]
public sealed class GetTidpFileGridController : ApiControllerBase
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
        Ok(await Dispatcher.Query(new GetTidpFileGridQuery(id, sheet, page, pageSize), ct));
}
