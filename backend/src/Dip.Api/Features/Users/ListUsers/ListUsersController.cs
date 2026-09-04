using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Users.ListUsers;

[Route("api/users")]
[Authorize]
public sealed class ListUsersController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<UserListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<UserListItem>>> Get(
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await Dispatcher.Query(new ListUsersQuery(search), ct);
        return Ok(result);
    }
}
