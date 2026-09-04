using Dip.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Auth.Login;

[Route("api/auth/login")]
public sealed class LoginController : ApiControllerBase
{
    public sealed record LoginRequest(string Email, string Password);

    [HttpPost]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResult>> Post([FromBody] LoginRequest body, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await Dispatcher.Send(new LoginCommand(body.Email, body.Password, ip), ct);
        return Ok(result);
    }
}
