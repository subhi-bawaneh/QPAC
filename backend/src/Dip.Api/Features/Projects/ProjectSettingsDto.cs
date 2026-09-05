using Dip.Domain.Enums;

namespace Dip.Api.Features.Projects;

// The settings that change what the engines compute (PLAN.md § 5.2, § 7).
public sealed record ProjectSettingsDto(
    Guid Id,
    string Code,
    string Name,
    string Client,
    string Organisation,
    string Approver,
    ScheduleMode ScheduleMode,
    int WorkingPlanApprovalDays,
    DateTime? ReportDate,
    DateTime BaselineStartDate,
    decimal WeightPending,
    decimal WeightSub1,
    decimal WeightSub2,
    decimal WeightApproved);
