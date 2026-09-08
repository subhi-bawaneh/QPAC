using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Lists.DeletePicklistItem;

[Route("api/picklists/{id:guid}")]
[Authorize]
public sealed class DeletePicklistItemController : ApiControllerBase
{
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Dispatcher.Send(new DeletePicklistItemCommand(id), ct);
        return NoContent();
    }
}
