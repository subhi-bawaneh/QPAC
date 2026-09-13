using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Tidps.GetTidpFolder;

[Route("api/projects/{projectId:guid}/tidp-folder")]
[Authorize]
public sealed class GetTidpFolderController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(TidpFolderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TidpFolderDto>> Get(
        Guid projectId, CancellationToken ct = default) =>
        Ok(await Dispatcher.Query(new GetTidpFolderQuery(projectId), ct));
}
