using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Projects.UpdateProjectSettings;

// Changing any of these changes what every report says, so the response tells the
// caller the snapshots need rebuilding.
[Permission(Permissions.ProjectSettings)]
public sealed record UpdateProjectSettingsCommand(
    Guid ProjectId,
    ScheduleMode ScheduleMode,
    int WorkingPlanApprovalDays,
    DateTime? ReportDate,
    decimal WeightPending,
    decimal WeightSub1,
    decimal WeightSub2,
    decimal WeightApproved) : ICommand<UpdateProjectSettingsResult>;

public sealed record UpdateProjectSettingsResult(
    ProjectSettingsDto Settings,
    bool RecalculationRequired);
