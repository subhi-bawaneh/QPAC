using Dip.Api.Common;
using Dip.Api.Features.Lists.GetStatusMappings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Lists.RestoreStatusMapping;

[Route("api/status-mappings/{id:guid}/restore")]
[Authorize]
public sealed class RestoreStatusMappingController : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(StatusMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StatusMappingDto>> Post(Guid id, CancellationToken ct) =>
        Ok(await Dispatcher.Send(new RestoreStatusMappingCommand(id), ct));
}
