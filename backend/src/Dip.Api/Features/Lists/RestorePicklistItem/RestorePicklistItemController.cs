using Dip.Api.Common;
using Dip.Api.Features.Lists.GetPicklists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Lists.RestorePicklistItem;

[Route("api/picklists/{id:guid}/restore")]
[Authorize]
public sealed class RestorePicklistItemController : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PicklistItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PicklistItemDto>> Post(Guid id, CancellationToken ct) =>
        Ok(await Dispatcher.Send(new RestorePicklistItemCommand(id), ct));
}
