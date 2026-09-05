using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Tracker.GetTrackerDocument;

[Route("api/tracker/documents/{documentId:guid}")]
[Authorize]
public sealed class GetTrackerDocumentController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(TrackerDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrackerDocumentDto>> Get(Guid documentId, CancellationToken ct)
    {
        var result = await Dispatcher.Query(new GetTrackerDocumentQuery(documentId), ct);
        return Ok(result);
    }
}
