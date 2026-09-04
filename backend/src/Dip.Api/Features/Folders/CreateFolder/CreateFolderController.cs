using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.CreateFolder;

[Route("api/folders")]
[Authorize]
public sealed class CreateFolderController : ApiControllerBase
{
    public sealed record CreateFolderRequest(Guid ProjectId, Guid? ParentId, string Name);

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Guid>> Post([FromBody] CreateFolderRequest body, CancellationToken ct)
    {
        var id = await Dispatcher.Send(new CreateFolderCommand(body.ProjectId, body.ParentId, body.Name), ct);
        return CreatedAtAction("Get", "GetFolder", new { id }, id);
    }
}
