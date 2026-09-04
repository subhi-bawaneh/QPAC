using Dip.Domain.Common;
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public class Project : Entity
{
    public string Code { get; set; } = string.Empty;                 // "QF01012"
    public string Name { get; set; } = string.Empty;                 // "Qiddiya Performing Arts Center"
    public string Client { get; set; } = string.Empty;
    public string Organisation { get; set; } = string.Empty;
    public string Approver { get; set; } = string.Empty;
    public string? CostCenter { get; set; }

    public ScheduleMode ScheduleMode { get; set; } = ScheduleMode.Baseline;
    public int WorkingPlanApprovalDays { get; set; } = 28;            // per PLAN.md § 5.2

    public DateTime? ReportDate { get; set; }                          // null => Today when engine runs
    public DateTime BaselineStartDate { get; set; } = new(2025, 11, 4);

    // Progress weights (PV / EV): Pending / Sub1 / Sub2 / Approved.
    // Defaults verified in Corporate Summary row 6: 0, 0.6, 0.9, 1.0.
    public decimal WeightPending { get; set; } = 0m;
    public decimal WeightSub1 { get; set; } = 0.6m;
    public decimal WeightSub2 { get; set; } = 0.9m;
    public decimal WeightApproved { get; set; } = 1.0m;
}

public class Discipline : Entity
{
    public Guid ProjectId { get; set; }
    public string Code { get; set; } = string.Empty;                   // "STL"
    public string CorporateName { get; set; } = string.Empty;          // "Structural"

    public Project? Project { get; set; }
}
