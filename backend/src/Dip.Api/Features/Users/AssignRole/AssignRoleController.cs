using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Users.AssignRole;

[Route("api/users/{id:guid}/roles")]
[Authorize]
public sealed class AssignRoleController : ApiControllerBase
{
    public sealed record AssignRolesRequest(string[] Roles);

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Put(Guid id, [FromBody] AssignRolesRequest body, CancellationToken ct)
    {
        await Dispatcher.Send(new AssignRoleCommand(id, body.Roles), ct);
        return NoContent();
    }
}
