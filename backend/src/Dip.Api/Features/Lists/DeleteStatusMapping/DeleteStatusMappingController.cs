using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Lists.DeleteStatusMapping;

[Route("api/status-mappings/{id:guid}")]
[Authorize]
public sealed class DeleteStatusMappingController : ApiControllerBase
{
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Dispatcher.Send(new DeleteStatusMappingCommand(id), ct);
        return NoContent();
    }
}
