using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Drafts.BulkUpdateDraftDocuments;

[Route("api/drafts/documents/bulk")]
[Authorize]
public sealed class BulkUpdateDraftDocumentsController : ApiControllerBase
{
    public sealed record BulkUpdateRequest(IReadOnlyList<Guid> Ids, BulkDraftFields Fields);

    [HttpPut]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<int>> Put([FromBody] BulkUpdateRequest body, CancellationToken ct)
    {
        var updated = await Dispatcher.Send(
            new BulkUpdateDraftDocumentsCommand(body.Ids ?? Array.Empty<Guid>(), body.Fields), ct);
        return Ok(updated);
    }
}
