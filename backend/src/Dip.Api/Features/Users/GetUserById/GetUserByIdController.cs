using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Users.GetUserById;

[Route("api/users/{id:guid}")]
[Authorize]
public sealed class GetUserByIdController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(UserDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDetail>> Get(Guid id, CancellationToken ct)
    {
        var result = await Dispatcher.Query(new GetUserByIdQuery(id), ct);
        return Ok(result);
    }
}
