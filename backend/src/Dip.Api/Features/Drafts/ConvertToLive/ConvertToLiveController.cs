using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Drafts.ConvertToLive;

[Route("api/folders/{id:guid}/convert-to-live")]
[Authorize]
public sealed class ConvertToLiveController : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ConvertToLiveResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConvertToLiveResultDto>> Post(Guid id, CancellationToken ct) =>
        Ok(await Dispatcher.Send(new ConvertToLiveCommand(id), ct));
}
