using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Tidps.ListTidpFiles;

[Route("api/projects/{projectId:guid}/tidp-files")]
[Authorize]
public sealed class ListTidpFilesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<TidpFileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<TidpFileDto>>> Get(
        Guid projectId, CancellationToken ct = default) =>
        Ok(await Dispatcher.Query(new ListTidpFilesQuery(projectId), ct));
}
