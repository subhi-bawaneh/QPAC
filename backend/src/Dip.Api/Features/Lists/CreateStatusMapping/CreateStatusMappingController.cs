using Dip.Api.Common;
using Dip.Api.Features.Lists.GetStatusMappings;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Lists.CreateStatusMapping;

[Route("api/projects/{projectId:guid}/status-mappings")]
[Authorize]
public sealed class CreateStatusMappingController : ApiControllerBase
{
    public sealed record CreateRequest(string AconexStatus, UnifiedStatus Status, bool IsLegacy);

    [HttpPost]
    [ProducesResponseType(typeof(StatusMappingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CreateStatusMappingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(
        Guid projectId, [FromBody] CreateRequest body, CancellationToken ct)
    {
        var result = await Dispatcher.Send(
            new CreateStatusMappingCommand(projectId, body.AconexStatus, body.Status, body.IsLegacy), ct);

        return result.Restored
            ? Ok(result)
            : StatusCode(StatusCodes.Status201Created, result.Item);
    }
}
