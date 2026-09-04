using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.GetFolder;

[Route("api/folders/{id:guid}")]
[Authorize]
public sealed class GetFolderController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(FolderDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FolderDetail>> Get(Guid id, CancellationToken ct)
    {
        var result = await Dispatcher.Query(new GetFolderQuery(id), ct);
        return Ok(result);
    }
}
