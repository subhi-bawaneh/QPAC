using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Projects.UpdateProjectSettings;

public sealed class UpdateProjectSettingsHandler
    : ICommandHandler<UpdateProjectSettingsCommand, UpdateProjectSettingsResult>
{
    private readonly DipDbContext _db;

    public UpdateProjectSettingsHandler(DipDbContext db) => _db = db;

    public async Task<UpdateProjectSettingsResult> Handle(
        UpdateProjectSettingsCommand command, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == command.ProjectId, ct)
            ?? throw new KeyNotFoundException($"Project {command.ProjectId} not found");

        // The schedule mode and the approval days feed the planned dates that every
        // snapshot stores, so changing either leaves the stored ones wrong until a
        // recalculation runs. The weights are applied at report time and do not.
        var schedulingChanged =
            project.ScheduleMode != command.ScheduleMode
            || project.WorkingPlanApprovalDays != command.WorkingPlanApprovalDays;

        project.ScheduleMode = command.ScheduleMode;
        project.WorkingPlanApprovalDays = command.WorkingPlanApprovalDays;
        project.ReportDate = command.ReportDate;
        project.WeightPending = command.WeightPending;
        project.WeightSub1 = command.WeightSub1;
        project.WeightSub2 = command.WeightSub2;
        project.WeightApproved = command.WeightApproved;

        var settings = new ProjectSettingsDto(
            project.Id, project.Code, project.Name, project.Client, project.Organisation,
            project.Approver, project.ScheduleMode, project.WorkingPlanApprovalDays,
            project.ReportDate, project.BaselineStartDate,
            project.WeightPending, project.WeightSub1, project.WeightSub2, project.WeightApproved);

        return new UpdateProjectSettingsResult(settings, schedulingChanged);
    }
}
