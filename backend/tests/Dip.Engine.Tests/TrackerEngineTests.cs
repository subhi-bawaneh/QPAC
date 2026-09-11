using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

public class TrackerEngineTests
{
    private const string DocNo = "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004";

    private static readonly StatusMappingLookup Statuses = StatusMappingLookup.Create(new[]
    {
        new StatusMapping { AconexStatus = "A - Approved", Status = UnifiedStatus.Approved },
        new StatusMapping { AconexStatus = "B - Approved with Comments", Status = UnifiedStatus.Approved },
        new StatusMapping { AconexStatus = "B - Approved with Comment", Status = UnifiedStatus.Approved, IsLegacy = true },
        new StatusMapping { AconexStatus = "C - Rejected", Status = UnifiedStatus.Rejected },
        new StatusMapping { AconexStatus = "Under Review", Status = UnifiedStatus.UnderReview },
        new StatusMapping { AconexStatus = "Terminated", Status = UnifiedStatus.Withdrawn },
    });

    private static Project Baseline() => new() { ScheduleMode = ScheduleMode.Baseline };

    private static Project WorkingPlan(int approvalDays = 28) => new()
    {
        ScheduleMode = ScheduleMode.WorkingPlan,
        WorkingPlanApprovalDays = approvalDays,
    };

    private static Document Doc(string? activityId = null, DateTime? milestone = null) => new()
    {
        Id = Guid.NewGuid(),
        DocumentNumber = DocNo,
        ActivityId = activityId,
        DeliveryMilestone = milestone,
    };

    private static AconexRevision Rev(
        string revision, DateTime dateModified, string status = "Under Review",
        bool terminated = false, string? transmittal = "BSBG-TRANSMIT-000156") => new()
    {
        DocNoFinal = DocNo,
        Revision = revision,
        DateModified = dateModified,
        AconexStatus = status,
        IsTerminated = terminated,
        TransmittalIn = transmittal,
    };

    // ---- which revision is "the" revision (RevisionOrder)

    // The defect: a reviewer responds to revision 00 on 14 March after revision 01 was
    // issued on 12 March. Ordering by DateModified alone reports the superseded
    // revision as the document's state.
    [Fact]
    public void Latest_PrefersHigherRevision_WhenLowerRevisionHasLaterResponse()
    {
        var row = Compute(Doc(), new[]
        {
            Rev("01", new DateTime(2025, 3, 12, 9, 0, 0), "Under Review"),
            Rev("00", new DateTime(2025, 3, 14, 17, 30, 0), "C - Rejected"),
        });

        row.Revision.Should().Be("01");
        row.AconexStatus.Should().Be("Under Review");
        row.DateModified.Should().Be(new DateTime(2025, 3, 12, 9, 0, 0));
    }

    [Fact]
    public void Latest_BreaksRevisionTie_ByDate()
    {
        var row = Compute(Doc(), new[]
        {
            Rev("02", new DateTime(2025, 5, 1, 8, 0, 0), "Under Review"),
            Rev("02", new DateTime(2025, 5, 9, 11, 45, 0), "A - Approved"),
        });

        row.Revision.Should().Be("02");
        row.AconexStatus.Should().Be("A - Approved");
        row.DateModified.Should().Be(new DateTime(2025, 5, 9, 11, 45, 0));
    }

    [Fact]
    public void Latest_RanksNumericAboveNonNumeric()
    {
        var row = Compute(Doc(), new[]
        {
            Rev("P01", new DateTime(2025, 6, 20, 10, 0, 0), "A - Approved"),
            Rev("00", new DateTime(2025, 6, 1, 10, 0, 0), "Under Review"),
        });

        row.Revision.Should().Be("00");
        row.SubmissionsCount.Should().Be(1);
    }

    // Submission Date is the first time the winning revision was seen, so picking a
    // different winner has to move it too.
    [Fact]
    public void SubmissionDate_FollowsTheWinningRevision()
    {
        var row = Compute(Doc(), new[]
        {
            Rev("01", new DateTime(2025, 3, 10, 9, 0, 0), "Under Review"),
            Rev("01", new DateTime(2025, 3, 12, 9, 0, 0), "Under Review"),
            Rev("00", new DateTime(2025, 3, 14, 17, 30, 0), "C - Rejected"),
        });

        row.SubmissionDate.Should().Be(new DateTime(2025, 3, 10, 9, 0, 0));
        row.ActualStart.Should().Be(new DateTime(2025, 3, 10, 9, 0, 0));
    }

    private static TrackerRow Compute(
        Document document, IEnumerable<AconexRevision> revisions,
        Project? project = null, IEnumerable<BaselineActivity>? baseline = null) =>
        TrackerEngine.ComputeOne(
            document,
            revisions.ToList(),
            BaselinePlan.Create(baseline ?? Array.Empty<BaselineActivity>()),
            Statuses,
            project ?? Baseline());

    [Fact]
    public void NoAconexRows_LeavesEveryComputedColumnNull()
    {
        var row = Compute(Doc(), Array.Empty<AconexRevision>());

        row.Revision.Should().BeNull();
        row.AconexStatus.Should().BeNull();
        row.Status.Should().BeNull();
        row.SubmissionsCount.Should().BeNull();
        row.DateModified.Should().BeNull();
        row.SubmissionDate.Should().BeNull();
        row.ActualStart.Should().BeNull();
        row.ActualFinish.Should().BeNull();
        row.Transmittal.Should().BeNull();
    }

    [Fact]
    public void LatestRow_IsTheOneWithMaxDateModified()
    {
        var row = Compute(Doc(), new[]
        {
            Rev("00", new DateTime(2026, 3, 31, 11, 35, 50, 696)),
            Rev("01", new DateTime(2026, 4, 26, 13, 4, 49, 41), "A - Approved"),
            Rev("00", new DateTime(2026, 4, 9, 11, 53, 0, 743)),
        });

        row.Revision.Should().Be("01");
        row.DateModified.Should().Be(new DateTime(2026, 4, 26, 13, 4, 49, 41));
        row.Status.Should().Be(UnifiedStatus.Approved);
    }

    [Fact]
    public void TiedRevisionAndDate_KeepsTheFirstRowInSourceOrder()
    {
        var tie = new DateTime(2026, 4, 26, 13, 4, 49, 41);

        var row = Compute(Doc(), new[]
        {
            Rev("01", tie, "A - Approved", transmittal: "FIRST"),
            Rev("01", tie, "C - Rejected", transmittal: "SECOND"),
        });

        row.Revision.Should().Be("01");
        row.Transmittal.Should().Be("FIRST");
    }

    // Two revisions carrying the same timestamp is not a tie: the revision decides.
    [Fact]
    public void TiedDateAcrossRevisions_TakesTheHigherRevision()
    {
        var tie = new DateTime(2026, 4, 26, 13, 4, 49, 41);

        var row = Compute(Doc(), new[]
        {
            Rev("01", tie, "A - Approved", transmittal: "FIRST"),
            Rev("02", tie, "C - Rejected", transmittal: "SECOND"),
        });

        row.Revision.Should().Be("02");
        row.Transmittal.Should().Be("SECOND");
    }

    [Fact]
    public void TerminatedRow_OverridesTheAconexStatus()
    {
        var row = Compute(Doc(), new[]
        {
            Rev("01", new DateTime(2026, 4, 26, 13, 0, 0), "A - Approved", terminated: true),
        });

        row.AconexStatus.Should().Be("Terminated");
        row.Status.Should().Be(UnifiedStatus.Withdrawn);
        row.ActualFinish.Should().BeNull("a terminated document is Withdrawn, not Approved");
    }

    [Theory]
    [InlineData("00", 1)]
    [InlineData("01", 2)]
    [InlineData("12", 13)]
    public void SubmissionsCount_IsTheNumericRevisionPlusOne(string revision, int expected)
    {
        var row = Compute(Doc(), new[] { Rev(revision, new DateTime(2026, 4, 1, 0, 0, 0)) });

        row.SubmissionsCount.Should().Be(expected);
    }

    [Theory]
    [InlineData("P00")]
    [InlineData("A")]
    [InlineData("")]
    public void SubmissionsCount_IsNullWhenTheRevisionIsNotNumeric(string revision)
    {
        var row = Compute(Doc(), new[] { Rev(revision, new DateTime(2026, 4, 1, 0, 0, 0)) });

        row.SubmissionsCount.Should().BeNull();
    }

    // Submission Date is the first appearance of the LATEST revision, not of the document.
    [Fact]
    public void SubmissionDate_IsTheEarliestDateOfTheLatestRevisionOnly()
    {
        var row = Compute(Doc(), new[]
        {
            Rev("00", new DateTime(2026, 3, 31, 11, 35, 50, 696)),
            Rev("01", new DateTime(2026, 4, 18, 10, 20, 36, 551)),
            Rev("01", new DateTime(2026, 4, 26, 13, 4, 49, 41)),
        });

        row.SubmissionDate.Should().Be(new DateTime(2026, 4, 18, 10, 20, 36, 551));
        row.ActualStart.Should().Be(new DateTime(2026, 3, 31, 11, 35, 50, 696),
            "Actual Start spans every revision");
    }

    [Fact]
    public void ActualFinish_IsSetOnlyWhenTheUnifiedStatusIsApproved()
    {
        var approved = Compute(Doc(), new[]
        {
            Rev("01", new DateTime(2026, 4, 26, 13, 0, 0), "B - Approved with Comments"),
        });
        var rejected = Compute(Doc(), new[]
        {
            Rev("01", new DateTime(2026, 4, 26, 13, 0, 0), "C - Rejected"),
        });

        approved.ActualFinish.Should().Be(new DateTime(2026, 4, 26, 13, 0, 0));
        rejected.ActualFinish.Should().BeNull();
    }

    [Fact]
    public void UnknownAconexStatus_LeavesUnifiedStatusNull()
    {
        var row = Compute(Doc(), new[] { Rev("01", new DateTime(2026, 4, 1, 0, 0, 0), "Something Else") });

        row.AconexStatus.Should().Be("Something Else");
        row.Status.Should().BeNull();
        row.ActualFinish.Should().BeNull();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Transmittal_IsNullWhenTheExportWroteZeroOrBlank(string? transmittal)
    {
        var row = Compute(Doc(), new[]
        {
            Rev("01", new DateTime(2026, 4, 1, 0, 0, 0), transmittal: transmittal),
        });

        row.Transmittal.Should().BeNull();
    }

    // Planned Start = the Submittal activity's Finish; Planned Finish = the Approval
    // activity of the same PACKAGE, which carries a different activity code.
    [Fact]
    public void BaselineMode_PlansFromTheSubmittalAndTheApprovalOfTheSamePackage()
    {
        var baseline = new[]
        {
            new BaselineActivity
            {
                ActivityCode = "QP.E.ST.GEN.GEN.1000",
                Package = "QP | QP.E | QP.E.ST",
                Type = BaselineActivityType.Submittal,
                Finish = new DateTime(2025, 12, 14),
            },
            new BaselineActivity
            {
                ActivityCode = "QP.E.ST.GEN.GEN.1010",
                Package = "QP | QP.E | QP.E.ST",
                Type = BaselineActivityType.Approval,
                Finish = new DateTime(2025, 12, 28),
            },
        };

        var row = Compute(Doc(activityId: "QP.E.ST.GEN.GEN.1000"),
            Array.Empty<AconexRevision>(), Baseline(), baseline);

        row.PlannedStart.Should().Be(new DateTime(2025, 12, 14));
        row.PlannedFinish.Should().Be(new DateTime(2025, 12, 28));
    }

    [Fact]
    public void BaselineMode_UnknownActivityId_LeavesBothPlannedDatesNull()
    {
        var row = Compute(Doc(activityId: "QP.NOT.THERE"), Array.Empty<AconexRevision>(), Baseline());

        row.PlannedStart.Should().BeNull();
        row.PlannedFinish.Should().BeNull();
    }

    [Fact]
    public void WorkingPlanMode_PlansFromTheDeliveryMilestonePlusApprovalDays()
    {
        var row = Compute(
            Doc(milestone: new DateTime(2025, 12, 19)),
            Array.Empty<AconexRevision>(),
            WorkingPlan());

        row.PlannedStart.Should().Be(new DateTime(2025, 12, 19));
        row.PlannedFinish.Should().Be(new DateTime(2026, 1, 16));
    }

    [Fact]
    public void Compute_MatchesAconexRowsToDocumentsByNumber()
    {
        var mine = new Document { Id = Guid.NewGuid(), DocumentNumber = DocNo };
        var other = new Document { Id = Guid.NewGuid(), DocumentNumber = "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0005" };

        var rows = TrackerEngine.Compute(
            new[] { mine, other },
            new[] { Rev("03", new DateTime(2026, 5, 1, 9, 0, 0), "A - Approved") },
            Array.Empty<BaselineActivity>(),
            new[] { new StatusMapping { AconexStatus = "A - Approved", Status = UnifiedStatus.Approved } },
            Baseline());

        rows.Single(r => r.DocumentId == mine.Id).Revision.Should().Be("03");
        rows.Single(r => r.DocumentId == other.Id).Revision.Should().BeNull();
    }

    [Fact]
    public void ToSnapshot_CarriesEveryComputedColumn()
    {
        var document = Doc();
        var projectId = Guid.NewGuid();
        var computedAt = new DateTime(2026, 9, 5, 8, 0, 0);
        var row = Compute(document, new[]
        {
            Rev("01", new DateTime(2026, 4, 26, 13, 4, 49, 41), "A - Approved"),
        });

        var snapshot = row.ToSnapshot(projectId, computedAt);

        snapshot.DocumentId.Should().Be(document.Id);
        snapshot.ProjectId.Should().Be(projectId);
        snapshot.ComputedAt.Should().Be(computedAt);
        snapshot.Revision.Should().Be("01");
        snapshot.Status.Should().Be(UnifiedStatus.Approved);
        snapshot.DateModified.Should().Be(row.DateModified);
        snapshot.ActualFinish.Should().Be(row.ActualFinish);
    }

    // Tracker.xlsx blanks the Document No Final of a terminated row, so every
    // aggregation skips it — including the minimums that Submission Date and
    // Actual Start are built from.
    [Fact]
    public void TerminatedRevisions_AreExcludedFromEveryAggregation()
    {
        var document = Doc();
        var revisions = new[]
        {
            Rev("02", new DateTime(2026, 7, 8, 15, 5, 34, 966), terminated: true),
            Rev("02", new DateTime(2026, 7, 8, 15, 10, 39, 722)),
            Rev("02", new DateTime(2026, 7, 25, 18, 0, 51, 0), "B - Approved with Comments"),
        };

        var row = TrackerEngine.Compute(
            new[] { document }, revisions,
            Array.Empty<BaselineActivity>(),
            new[]
            {
                new StatusMapping { AconexStatus = "B - Approved with Comments", Status = UnifiedStatus.Approved },
            },
            Baseline()).Single();

        row.SubmissionDate.Should().Be(new DateTime(2026, 7, 8, 15, 10, 39, 722));
        row.ActualStart.Should().Be(new DateTime(2026, 7, 8, 15, 10, 39, 722));
        row.DateModified.Should().Be(new DateTime(2026, 7, 25, 18, 0, 51, 0));
    }

    [Fact]
    public void DocumentWithOnlyTerminatedRevisions_HasNoAconexData()
    {
        var document = Doc();

        var row = TrackerEngine.Compute(
            new[] { document },
            new[] { Rev("00", new DateTime(2026, 7, 8, 15, 0, 0), terminated: true) },
            Array.Empty<BaselineActivity>(),
            Array.Empty<StatusMapping>(),
            Baseline()).Single();

        row.Revision.Should().BeNull();
        row.DateModified.Should().BeNull();
    }
}
