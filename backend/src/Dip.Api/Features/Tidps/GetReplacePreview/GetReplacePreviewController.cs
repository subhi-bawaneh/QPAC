using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Tidps.GetReplacePreview;

[Route("api/tidp-files/{id:guid}/replace-preview")]
[Authorize]
public sealed class GetReplacePreviewController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ReplacePreview), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReplacePreview>> Get(Guid id, CancellationToken ct = default) =>
        Ok(await Dispatcher.Query(new GetReplacePreviewQuery(id), ct));
}
