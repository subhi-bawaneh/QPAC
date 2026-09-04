using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Auth.Me;

[Route("api/me")]
[Authorize]
public sealed class MeController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(UserSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserSummary>> Get(CancellationToken ct)
    {
        var result = await Dispatcher.Query(new MeQuery(), ct);
        return Ok(result);
    }
}
