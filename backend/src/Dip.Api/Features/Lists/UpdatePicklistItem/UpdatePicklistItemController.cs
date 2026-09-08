using Dip.Api.Common;
using Dip.Api.Features.Lists.GetPicklists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Lists.UpdatePicklistItem;

[Route("api/picklists/{id:guid}")]
[Authorize]
public sealed class UpdatePicklistItemController : ApiControllerBase
{
    public sealed record UpdateRequest(string Code, string Description, int SortOrder);

    [HttpPut]
    [ProducesResponseType(typeof(PicklistItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PicklistItemDto>> Put(
        Guid id, [FromBody] UpdateRequest body, CancellationToken ct) =>
        Ok(await Dispatcher.Send(
            new UpdatePicklistItemCommand(id, body.Code, body.Description ?? string.Empty, body.SortOrder), ct));
}
