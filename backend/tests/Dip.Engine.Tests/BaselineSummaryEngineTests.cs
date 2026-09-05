using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

public class BaselineSummaryEngineTests
{
    private sealed record Row(
        string Discipline = "Structural",
        string? ActivityId = null,
        DateTime? ActualStart = null,
        DateTime? ActualFinish = null,
        string? AconexStatus = null,
        UnifiedStatus? Status = null);

    private static BaselineSummary Compute(
        IEnumerable<Row> rows, params BaselineActivity[] baseline)
    {
        var documents = new List<Document>();
        var trackerRows = new List<TrackerRow>();

        foreach (var row in rows)
        {
            var document = new Document
            {
                Id = Guid.NewGuid(),
                DocumentNumber = $"DOC-{documents.Count:0000}",
                CorporateDiscipline = row.Discipline,
                ActivityId = row.ActivityId,
            };
            documents.Add(document);
            trackerRows.Add(new TrackerRow(
                document.Id, document.DocumentNumber,
                SubmissionsCount: null, Revision: null, AconexStatus: row.AconexStatus,
                Status: row.Status, SubmissionDate: null, DateModified: null, Transmittal: null,
                PlannedStart: null, PlannedFinish: null,
                ActualStart: row.ActualStart, ActualFinish: row.ActualFinish));
        }

        return BaselineSummaryEngine.Compute(documents, trackerRows, baseline);
    }

    private static BaselineActivity Submittal(string code, string package = "PKG") => new()
    {
        ActivityCode = code,
        Package = package,
        Type = BaselineActivityType.Submittal,
    };

    private static BaselineActivity Approval(string code, string package = "PKG") => new()
    {
        ActivityCode = code,
        Package = package,
        Type = BaselineActivityType.Approval,
    };

    private static readonly DateTime SomeDate = new(2026, 4, 1);

    // Submitted/Approved are date-driven, matching the sheet's COUNTIFS on
    // Actual Start / Actual Finish rather than on the unified status.
    [Fact]
    public void DisciplineCounts_AreDrivenByTheActualDates()
    {
        var summary = Compute(new[]
        {
            new Row(ActualStart: SomeDate, ActualFinish: SomeDate),
            new Row(ActualStart: SomeDate),
            new Row(),
        });

        var structural = summary.Disciplines.Single();
        structural.Total.Should().Be(3);
        structural.Submitted.Should().Be(2);
        structural.Approved.Should().Be(1);
    }

    [Fact]
    public void DisciplineCounts_SplitTheReviseAndRejectStatuses()
    {
        var summary = Compute(new[]
        {
            new Row(AconexStatus: BaselineSummaryEngine.CReviseStatus),
            new Row(AconexStatus: BaselineSummaryEngine.DRejectedStatus),
            new Row(AconexStatus: "A - Approved"),
            new Row(Status: UnifiedStatus.UnderReview),
        });

        var structural = summary.Disciplines.Single();
        structural.CRevise.Should().Be(1);
        structural.DRejected.Should().Be(1);
        structural.UnderReview.Should().Be(1);
    }

    // The status text is matched after trimming and collapsing whitespace, so the
    // spacing differences between exports don't silently zero the columns — which
    // is exactly what happens in the workbook's own formula.
    [Fact]
    public void ReviseStatus_MatchesRegardlessOfSpacingAndCase()
    {
        var summary = Compute(new[]
        {
            new Row(AconexStatus: "c  -  revise and resubmit"),
            new Row(AconexStatus: " C - Revise and Resubmit "),
        });

        summary.Disciplines.Single().CRevise.Should().Be(2);
    }

    [Fact]
    public void Totals_CountEveryDocumentAcrossDisciplines()
    {
        var summary = Compute(new[]
        {
            new Row("Structural", ActualStart: SomeDate),
            new Row("Electrical"),
            new Row("Electrical", ActualStart: SomeDate, ActualFinish: SomeDate),
        });

        summary.Total.Name.Should().Be(BaselineSummaryEngine.TotalRowName);
        summary.Total.Total.Should().Be(3);
        summary.Total.Submitted.Should().Be(2);
        summary.Total.Approved.Should().Be(1);
        summary.Disciplines.Select(d => d.Name).Should().Equal("Electrical", "Structural");
    }

    [Theory]
    [InlineData(0, 0, PackageStatus.Unused)]
    [InlineData(5, 0, PackageStatus.Pending)]
    [InlineData(5, 2, PackageStatus.Partial)]
    [InlineData(5, 5, PackageStatus.Submitted)]
    public void PackageStatus_FollowsTheSubmittedShare(int total, int submitted, PackageStatus expected)
    {
        BaselineSummaryEngine.StatusOf(total, submitted).Should().Be(expected);
    }

    [Fact]
    public void Packages_AreOneRowPerSubmittalActivity_WithApprovalRowsIgnored()
    {
        var summary = Compute(
            new[]
            {
                new Row(ActivityId: "QP.A.1000", ActualStart: SomeDate),
                new Row(ActivityId: "QP.A.1000"),
                new Row(ActivityId: "QP.B.1000"),
            },
            Submittal("QP.A.1000", "PKG-A"),
            Approval("QP.A.1010", "PKG-A"),
            Submittal("QP.B.1000", "PKG-B"),
            Submittal("QP.C.1000", "PKG-C"));

        summary.Packages.Select(p => p.ActivityCode)
            .Should().Equal("QP.A.1000", "QP.B.1000", "QP.C.1000");

        var a = summary.Packages.Single(p => p.ActivityCode == "QP.A.1000");
        a.Package.Should().Be("PKG-A");
        a.Total.Should().Be(2);
        a.Submitted.Should().Be(1);
        a.Status.Should().Be(PackageStatus.Partial);

        summary.Packages.Single(p => p.ActivityCode == "QP.B.1000").Status.Should().Be(PackageStatus.Pending);
        summary.Packages.Single(p => p.ActivityCode == "QP.C.1000").Status.Should().Be(PackageStatus.Unused);
    }

    [Fact]
    public void PackageStatusRollup_CountsPackagesAndTheirDrawings()
    {
        var summary = Compute(
            new[]
            {
                new Row(ActivityId: "QP.A.1000", ActualStart: SomeDate),
                new Row(ActivityId: "QP.B.1000"),
                new Row(ActivityId: "QP.B.1000"),
            },
            Submittal("QP.A.1000"),
            Submittal("QP.B.1000"),
            Submittal("QP.C.1000"));

        summary.TotalPackages.Should().Be(3);
        summary.TotalPackageDrawings.Should().Be(3);

        Count(PackageStatus.Submitted).Should().Be((1, 1));
        Count(PackageStatus.Pending).Should().Be((1, 2));
        Count(PackageStatus.Unused).Should().Be((1, 0));
        Count(PackageStatus.Partial).Should().Be((0, 0));

        (int Packages, int Drawings) Count(PackageStatus status)
        {
            var row = summary.PackageStatuses.Single(s => s.Status == status);
            return (row.Packages, row.Drawings);
        }
    }

    // Documents whose Activity ID matches no baseline package are counted in their
    // discipline but not against any package — the sheet's package drawings total
    // (15,744) is smaller than its document total (15,883) for exactly this reason.
    [Fact]
    public void DocumentsWithAnUnknownActivityId_CountInDisciplinesButNoPackage()
    {
        var summary = Compute(
            new[]
            {
                new Row(ActivityId: "QP.A.1000"),
                new Row(ActivityId: "NOT.A.PACKAGE"),
                new Row(ActivityId: null),
            },
            Submittal("QP.A.1000"));

        summary.Total.Total.Should().Be(3);
        summary.TotalPackageDrawings.Should().Be(1);
    }

    [Fact]
    public void DocumentWithNoTrackerRow_CountsAsNotSubmitted()
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            CorporateDiscipline = "Landscape",
            ActivityId = "QP.A.1000",
        };

        var summary = BaselineSummaryEngine.Compute(
            new[] { document }, Array.Empty<TrackerRow>(), new[] { Submittal("QP.A.1000") });

        summary.Total.Total.Should().Be(1);
        summary.Total.Submitted.Should().Be(0);
        summary.Packages.Single().Status.Should().Be(PackageStatus.Pending);
    }
}
