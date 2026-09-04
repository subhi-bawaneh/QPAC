using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Infrastructure.Seeding;

// Static seed data — kept out of the migration so it can evolve without SQL churn.
// Values sourced from docs/excel-analysis.md.
internal static class SeedData
{
    public const string QpacProjectCode = "QF01012";

    public static Project QpacProject() => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Code = QpacProjectCode,
        Name = "Qiddiya Performing Arts Center",
        Client = "Qiddiya",
        Organisation = "Nesma & Partners",
        Approver = "BSBG",
        CostCenter = "322151",
        ScheduleMode = ScheduleMode.Baseline,
        WorkingPlanApprovalDays = 28,
        BaselineStartDate = new DateTime(2025, 11, 4),
    };

    // 9 disciplines observed in Corporate Summary (docs/excel-analysis.md § 4.4).
    // Short codes chosen to match F05 (STL/STR/ARC/ELE/MEC/FIR/INF/FAC + INT/LSC for design/landscape).
    public static IReadOnlyList<(string Code, string CorporateName)> Disciplines { get; } =
    [
        ("ARC", "Architectural"),
        ("ELE", "Electrical"),
        ("FAC", "Façade"),
        ("FLS", "Fire & Life Safety"),
        ("INF", "Infrastructure"),
        ("INT", "Interior Design"),
        ("LSC", "Landscape"),
        ("MEC", "Mechanical"),
        ("STR", "Structural"),
        // Short codes present in TIDP files but rolled up into Structural — kept for lookup.
        ("STL", "Structural"),
    ];

    // Status mapping — final list from docs/excel-analysis.md § 6b (22 entries).
    // The Aconex data itself uses "B - Approved with Comments" (plural);
    // the legacy Tracker Lists sheet uses "Comment" (singular). Both seeded.
    public static IReadOnlyList<(string AconexStatus, UnifiedStatus Status, bool IsLegacy)> StatusMappings { get; } =
    [
        ("A - Approved", UnifiedStatus.Approved, false),
        ("B - Approved with Comments", UnifiedStatus.Approved, false),
        ("B - Approved with Comment", UnifiedStatus.Approved, true),
        ("C - Revise and Resubmit", UnifiedStatus.Rejected, false),
        ("D - Rejected", UnifiedStatus.Rejected, false),
        ("E - Review Not Required", UnifiedStatus.Approved, false),
        ("Issued For Approval", UnifiedStatus.UnderReview, false),
        ("Issued for Action", UnifiedStatus.UnderReview, false),
        ("Issued for information", UnifiedStatus.Approved, false),
        ("For Review", UnifiedStatus.UnderReview, false),
        ("No Longer In Use", UnifiedStatus.Withdrawn, false),
        ("No Longer in Use", UnifiedStatus.Withdrawn, true),
        ("Under Review", UnifiedStatus.UnderReview, false),
        ("QA Rejected", UnifiedStatus.Rejected, false),
        ("QA Checked", UnifiedStatus.UnderReview, false),
        ("Terminated", UnifiedStatus.Withdrawn, false),
        ("Open", UnifiedStatus.UnderReview, false),
        ("Responded", UnifiedStatus.UnderReview, false),
        ("Submitted to Site", UnifiedStatus.UnderReview, true),
        ("Site Rejected", UnifiedStatus.Rejected, true),
        ("Submitted to Client", UnifiedStatus.UnderReview, true),
        ("For Information", UnifiedStatus.Approved, true),
    ];
}
