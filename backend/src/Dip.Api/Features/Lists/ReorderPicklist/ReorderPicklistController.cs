using Dip.Api.Common;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Lists.ReorderPicklist;

[Route("api/projects/{projectId:guid}/picklists/{field}/order")]
[Authorize]
public sealed class ReorderPicklistController : ApiControllerBase
{
    public sealed record ReorderRequest(IReadOnlyList<Guid> Ids);

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Put(
        Guid projectId, PicklistField field, [FromBody] ReorderRequest body, CancellationToken ct)
    {
        await Dispatcher.Send(
            new ReorderPicklistCommand(projectId, field, body.Ids ?? []), ct);
        return NoContent();
    }
}
