using Dip.Api.Common;
using Dip.Api.Features.Lists.GetPicklists;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Lists.CreatePicklistItem;

[Route("api/projects/{projectId:guid}/picklists")]
[Authorize]
public sealed class CreatePicklistItemController : ApiControllerBase
{
    public sealed record CreateRequest(PicklistField Field, string Code, string Description, int? SortOrder);

    // 201 for a new code, 200 when an existing soft-deleted row was restored.
    [HttpPost]
    [ProducesResponseType(typeof(PicklistItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CreatePicklistItemResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(
        Guid projectId, [FromBody] CreateRequest body, CancellationToken ct)
    {
        var result = await Dispatcher.Send(
            new CreatePicklistItemCommand(
                projectId, body.Field, body.Code, body.Description ?? string.Empty, body.SortOrder),
            ct);

        return result.Restored
            ? Ok(result)
            : StatusCode(StatusCodes.Status201Created, result.Item);
    }
}
