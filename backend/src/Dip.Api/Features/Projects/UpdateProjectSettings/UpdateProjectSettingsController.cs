using Dip.Api.Common;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Projects.UpdateProjectSettings;

[Route("api/projects/{projectId:guid}/settings")]
[Authorize]
public sealed class UpdateProjectSettingsController : ApiControllerBase
{
    public sealed record UpdateSettingsRequest(
        ScheduleMode ScheduleMode,
        int WorkingPlanApprovalDays,
        DateTime? ReportDate,
        decimal WeightPending,
        decimal WeightSub1,
        decimal WeightSub2,
        decimal WeightApproved);

    [HttpPut]
    [ProducesResponseType(typeof(UpdateProjectSettingsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpdateProjectSettingsResult>> Put(
        Guid projectId, [FromBody] UpdateSettingsRequest body, CancellationToken ct)
    {
        var result = await Dispatcher.Send(new UpdateProjectSettingsCommand(
            projectId, body.ScheduleMode, body.WorkingPlanApprovalDays, body.ReportDate,
            body.WeightPending, body.WeightSub1, body.WeightSub2, body.WeightApproved), ct);
        return Ok(result);
    }
}
