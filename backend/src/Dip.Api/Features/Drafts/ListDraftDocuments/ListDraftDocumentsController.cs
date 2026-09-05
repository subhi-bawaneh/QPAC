using Dip.Api.Common;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Drafts.ListDraftDocuments;

[Route("api/drafts/documents")]
[Authorize]
public sealed class ListDraftDocumentsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DraftDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<DraftDocumentDto>>> Get(
        [FromQuery] Guid folderFileId,
        [FromQuery] DraftRowState? state,
        [FromQuery] Guid? disciplineId,
        [FromQuery] bool? isDuplicate,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await Dispatcher.Query(
            new ListDraftDocumentsQuery(folderFileId, state, disciplineId, isDuplicate, search, page, pageSize), ct);
        return Ok(result);
    }
}
