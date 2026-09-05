using Dip.Api.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dip.Api.Features.Auth.Refresh;

[Route("api/auth/refresh")]
[EnableRateLimiting(DependencyInjection.AuthRateLimitPolicy)]
public sealed class RefreshController : ApiControllerBase
{
    public sealed record RefreshRequest(string RefreshToken);

    [HttpPost]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResult>> Post([FromBody] RefreshRequest body, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await Dispatcher.Send(new RefreshCommand(body.RefreshToken, ip), ct);
        return Ok(result);
    }
}
