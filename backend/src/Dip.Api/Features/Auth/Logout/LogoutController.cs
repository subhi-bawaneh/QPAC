using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Auth.Logout;

[Route("api/auth/logout")]
[Authorize]
public sealed class LogoutController : ApiControllerBase
{
    public sealed record LogoutRequest(string RefreshToken);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Post([FromBody] LogoutRequest body, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await Dispatcher.Send(new LogoutCommand(body.RefreshToken, ip), ct);
        return NoContent();
    }
}
