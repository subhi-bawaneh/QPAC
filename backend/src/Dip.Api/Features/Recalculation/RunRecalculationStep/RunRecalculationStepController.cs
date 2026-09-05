using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Recalculation.RunRecalculationStep;

[Route("api/projects/{projectId:guid}/recalculate/step")]
[Authorize]
public sealed class RunRecalculationStepController : ApiControllerBase
{
    public sealed record RecalculateRequest(int Offset = 0, int Take = RecalculationService.DefaultChunkSize);

    [HttpPost]
    [ProducesResponseType(typeof(RecalculationStepResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecalculationStepResult>> Post(
        Guid projectId, [FromBody] RecalculateRequest? body, CancellationToken ct)
    {
        var request = body ?? new RecalculateRequest();
        var result = await Dispatcher.Send(
            new RunRecalculationStepCommand(projectId, request.Offset, request.Take), ct);
        return Ok(result);
    }
}
