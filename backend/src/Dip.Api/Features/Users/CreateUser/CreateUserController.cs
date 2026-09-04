using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Users.CreateUser;

[Route("api/users")]
[Authorize]
public sealed class CreateUserController : ApiControllerBase
{
    public sealed record CreateUserRequest(string Email, string Password, string FullName, string[] Roles);

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Guid>> Post([FromBody] CreateUserRequest body, CancellationToken ct)
    {
        var id = await Dispatcher.Send(
            new CreateUserCommand(body.Email, body.Password, body.FullName, body.Roles),
            ct);
        return CreatedAtAction(actionName: "Get", controllerName: "GetUserById", routeValues: new { id }, value: id);
    }
}
