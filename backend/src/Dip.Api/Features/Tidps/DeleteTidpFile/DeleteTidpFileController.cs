using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Tidps.DeleteTidpFile;

[Route("api/tidp-files/{id:guid}")]
[Authorize]
public sealed class DeleteTidpFileController : ApiControllerBase
{
    [HttpDelete]
    [ProducesResponseType(typeof(DeleteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeleteResult>> Delete(Guid id, CancellationToken ct = default) =>
        Ok(await Dispatcher.Send(new DeleteTidpFileCommand(id), ct));
}
