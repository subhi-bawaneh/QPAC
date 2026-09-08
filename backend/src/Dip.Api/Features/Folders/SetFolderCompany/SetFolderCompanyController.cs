using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Folders.SetFolderCompany;

[Route("api/folders/{id:guid}/company")]
[Authorize]
public sealed class SetFolderCompanyController : ApiControllerBase
{
    public sealed record SetCompanyRequest(bool IsCompany, Guid? AuthorId);

    [HttpPut]
    [ProducesResponseType(typeof(FolderNode), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FolderNode>> Put(
        Guid id, [FromBody] SetCompanyRequest body, CancellationToken ct) =>
        Ok(await Dispatcher.Send(new SetFolderCompanyCommand(id, body.IsCompany, body.AuthorId), ct));
}
