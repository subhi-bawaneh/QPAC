using Dip.Api.Common;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Imports.StartImport;

[Route("api/imports/start")]
[Authorize]
public sealed class StartImportController : ApiControllerBase
{
    public sealed record StartRequest(Guid ProjectId, Guid FolderFileId, ImportKind Kind, DataTarget Target);

    [HttpPost]
    [ProducesResponseType(typeof(StartImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StartImportResult>> Post([FromBody] StartRequest body, CancellationToken ct)
    {
        var result = await Dispatcher.Send(
            new StartImportCommand(body.ProjectId, body.FolderFileId, body.Kind, body.Target),
            ct);
        return Ok(result);
    }
}
