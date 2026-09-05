using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Users.ListRoles;

[Route("api/roles")]
[Authorize]
public sealed class ListRolesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> Get(CancellationToken ct)
    {
        var result = await Dispatcher.Query(new ListRolesQuery(), ct);
        return Ok(result);
    }
}
