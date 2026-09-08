using Dip.Api.Common;
using Dip.Api.Features.Lists.GetStatusMappings;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Lists.UpdateStatusMapping;

[Route("api/status-mappings/{id:guid}")]
[Authorize]
public sealed class UpdateStatusMappingController : ApiControllerBase
{
    public sealed record UpdateRequest(string AconexStatus, UnifiedStatus Status, bool IsLegacy);

    [HttpPut]
    [ProducesResponseType(typeof(StatusMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StatusMappingDto>> Put(
        Guid id, [FromBody] UpdateRequest body, CancellationToken ct) =>
        Ok(await Dispatcher.Send(
            new UpdateStatusMappingCommand(id, body.AconexStatus, body.Status, body.IsLegacy), ct));
}
