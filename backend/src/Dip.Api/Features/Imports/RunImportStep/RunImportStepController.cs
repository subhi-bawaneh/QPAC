using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Imports.RunImportStep;

[Route("api/imports/{batchId:guid}/step")]
[Authorize]
public sealed class RunImportStepController : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(RunStepResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunStepResult>> Post(Guid batchId, CancellationToken ct)
    {
        var result = await Dispatcher.Send(new RunImportStepCommand(batchId), ct);
        return Ok(result);
    }
}
