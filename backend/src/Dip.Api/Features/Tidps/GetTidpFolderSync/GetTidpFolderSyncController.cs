using Dip.Api.Common;
using Dip.Api.Features.Tidps.SyncTidpFolder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Tidps.GetTidpFolderSync;

[Route("api/projects/{projectId:guid}/tidp-folder/syncs")]
[Authorize]
public sealed class GetTidpFolderSyncController : ApiControllerBase
{
    [HttpGet("{syncId:guid}")]
    [ProducesResponseType(typeof(TidpFolderSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TidpFolderSyncResult>> Get(
        Guid projectId, Guid syncId, CancellationToken ct = default) =>
        Ok(await Dispatcher.Query(new GetTidpFolderSyncQuery(projectId, syncId), ct));
}
