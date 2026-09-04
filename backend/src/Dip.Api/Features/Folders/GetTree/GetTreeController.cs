using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.GetTree;

[Route("api/projects/{projectId:guid}/folders/tree")]
[Authorize]
public sealed class GetTreeController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FolderTreeNode>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FolderTreeNode>>> Get(Guid projectId, CancellationToken ct)
    {
        var result = await Dispatcher.Query(new GetTreeQuery(projectId), ct);
        return Ok(result);
    }
}
