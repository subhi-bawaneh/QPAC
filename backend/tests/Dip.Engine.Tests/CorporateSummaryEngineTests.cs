using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

public class CorporateSummaryEngineTests
{
    private static readonly DateTime ReportDate = new(2026, 8, 30);        // a Sunday
    private static readonly DateTime CurrentWeek = new(2026, 8, 30, 23, 59, 59);

    private static Project Project() => new()
    {
        ScheduleMode = ScheduleMode.Baseline,
        ReportDate = ReportDate,
        BaselineStartDate = new DateTime(2025, 11, 4),
    };

    private sealed record Row(
        string Discipline,
        string? Author = "JINGGONG",
        DateTime? PlannedStart = null,
        DateTime? PlannedFinish = null,
        DateTime? ActualStart = null,
        DateTime? ActualFinish = null,
        UnifiedStatus? Status = null,
        int? Submissions = null);

    private static CorporateSummary Compute(params Row[] rows)
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
                Exchanges = new List<DataExchange> { new() { Number = 1, Author = row.Author } },
            };
            documents.Add(document);
            trackerRows.Add(new TrackerRow(
                document.Id, document.DocumentNumber,
                SubmissionsCount: row.Submissions, Revision: null, AconexStatus: null,
                Status: row.Status, SubmissionDate: null, DateModified: null, Transmittal: null,
                PlannedStart: row.PlannedStart, PlannedFinish: row.PlannedFinish,
                ActualStart: row.ActualStart, ActualFinish: row.ActualFinish));
        }

        return CorporateSummaryEngine.Compute(documents, trackerRows, Project());
    }

    [Fact]
    public void CurrentWeek_IsTheWeekEndOfTheReportDate()
    {
        var summary = Compute(new Row("Structural"));

        summary.ReportDate.Should().Be(ReportDate);
        summary.CurrentWeek.Should().Be(CurrentWeek);
    }

    [Fact]
    public void Progress_CountsDatesUpToTheCurrentWeekOnly()
    {
        var summary = Compute(
            new Row("Structural",
                PlannedStart: new DateTime(2026, 1, 1),
                ActualStart: new DateTime(2026, 2, 1),
                ActualFinish: new DateTime(2026, 3, 1)),
            new Row("Structural",
                PlannedStart: new DateTime(2027, 1, 1),      // after the report date
                ActualStart: new DateTime(2027, 2, 1),
                ActualFinish: new DateTime(2027, 3, 1)));

        var structural = summary.Disciplines.Single();
        structural.Total.Should().Be(2);
        structural.Planned.Should().Be(1);
        structural.Submitted.Should().Be(1);
        structural.Approved.Should().Be(1);
    }

    // Quality = Approved / (TotalRevisions - UnderReview), where
    // TotalRevisions = Σ SubmissionsCount - Withdrawn.
    [Fact]
    public void Quality_DividesApprovedByRevisionsExcludingUnderReview()
    {
        var summary = Compute(
            new Row("Electrical", Status: UnifiedStatus.Approved, Submissions: 2),
            new Row("Electrical", Status: UnifiedStatus.Approved, Submissions: 1),
            new Row("Electrical", Status: UnifiedStatus.Rejected, Submissions: 3),
            new Row("Electrical", Status: UnifiedStatus.UnderReview, Submissions: 2));

        var electrical = summary.Disciplines.Single();
        electrical.QualityApproved.Should().Be(2);
        electrical.Rejected.Should().Be(1);
        electrical.UnderReview.Should().Be(1);
        electrical.TotalRevisions.Should().Be(8);
        // 2 approved / (8 revisions - 1 under-review document) — the denominator
        // subtracts a document count, which is what reproduces the sheet's 0.6190
        // for Electrical (624 / (1054 - 46)).
        electrical.Quality.Should().BeApproximately(2m / 7m, 0.0001m);
    }

    [Fact]
    public void Quality_IsNullWhenTheDenominatorIsZero()
    {
        var summary = Compute(new Row("Interior Design"));

        var group = summary.Disciplines.Single();
        group.TotalRevisions.Should().Be(0);
        group.Quality.Should().BeNull();
    }

    [Fact]
    public void TotalRevisions_SubtractsWithdrawnDocuments()
    {
        var summary = Compute(
            new Row("Mechanical", Status: UnifiedStatus.Withdrawn, Submissions: 2),
            new Row("Mechanical", Status: UnifiedStatus.Approved, Submissions: 1));

        var group = summary.Disciplines.Single();
        group.Withdrawn.Should().Be(1);
        group.TotalRevisions.Should().Be(2, "3 revisions minus the withdrawn document");
    }

    // PV buckets: Approved = PlannedFinish <= C, Sub1 = the rest of what started,
    // Pending = everything not yet planned to start. Sub 2 is reserved and stays 0.
    [Fact]
    public void PlannedValue_SplitsIntoPendingSub1AndApproved()
    {
        var summary = Compute(
            new Row("Façade",
                PlannedStart: new DateTime(2026, 1, 1), PlannedFinish: new DateTime(2026, 2, 1)),
            new Row("Façade",
                PlannedStart: new DateTime(2026, 1, 1), PlannedFinish: new DateTime(2027, 2, 1)),
            new Row("Façade"),
            new Row("Façade", PlannedStart: new DateTime(2027, 1, 1)));

        var group = summary.Disciplines.Single();
        group.PvApproved.Should().Be(1);
        group.PvSub1.Should().Be(1);
        group.PvSub2.Should().Be(0);
        group.PvPending.Should().Be(2, "a null or future Planned Start is pending");
        group.PlannedPercent.Should().BeApproximately((1 * 0.6m + 1 * 1.0m) / 4, 0.0001m);
    }

    // EV buckets are driven by how many times a document was actually submitted.
    [Fact]
    public void EarnedValue_SplitsBySubmissionCountUnlessApproved()
    {
        var summary = Compute(
            new Row("Structural", Status: UnifiedStatus.Approved, Submissions: 3),
            new Row("Structural", Status: UnifiedStatus.UnderReview, Submissions: 1),
            new Row("Structural", Status: UnifiedStatus.Rejected, Submissions: 2),
            new Row("Structural"));

        var group = summary.Disciplines.Single();
        group.EvApproved.Should().Be(1);
        group.EvSub1.Should().Be(1);
        group.EvSub2.Should().Be(1);
        group.EvPending.Should().Be(1, "no submissions at all");
        group.CompletedPercent.Should().BeApproximately((0.6m + 0.9m + 1.0m) / 4, 0.0001m);
    }

    [Fact]
    public void Weeks_BucketDatesIntoTheWeekTheyFallIn_AndAccumulate()
    {
        var summary = Compute(
            new Row("Structural",
                PlannedStart: new DateTime(2025, 11, 5),      // week ending 2025-11-09
                ActualStart: new DateTime(2025, 11, 12),      // week ending 2025-11-16
                ActualFinish: new DateTime(2025, 11, 12)));

        var first = summary.Weeks.Single(w => w.To.Date == new DateTime(2025, 11, 9));
        var second = summary.Weeks.Single(w => w.To.Date == new DateTime(2025, 11, 16));

        first.Planned.Should().Be(1);
        first.Submitted.Should().Be(0);
        second.Planned.Should().Be(0);
        second.Submitted.Should().Be(1);
        second.Approved.Should().Be(1);
        second.CumulativePlanned.Should().Be(1);
        second.CumulativeSubmitted.Should().Be(1);
    }

    [Fact]
    public void Weeks_AreHalfOpen_ADateOnAWeekEndBelongsToThatWeek()
    {
        var summary = Compute(
            new Row("Structural", PlannedStart: new DateTime(2025, 11, 9, 12, 0, 0)),
            new Row("Structural", PlannedStart: new DateTime(2025, 11, 23)));   // extends the timeline

        summary.Weeks.Single(w => w.To.Date == new DateTime(2025, 11, 9)).Planned.Should().Be(1,
            "a date on the week end belongs to that week, not the next");
        summary.Weeks.Single(w => w.To.Date == new DateTime(2025, 11, 16)).Planned.Should().Be(0);
        summary.Weeks.Single(w => w.To.Date == new DateTime(2025, 11, 23)).Planned.Should().Be(1);
    }

    [Fact]
    public void Groups_AreProducedForDisciplinesAndAuthorsFromTheSameDocuments()
    {
        var summary = Compute(
            new Row("Structural", Author: "JINGGONG"),
            new Row("Electrical", Author: "JINGGONG"),
            new Row("Electrical", Author: "AFCO"));

        summary.Disciplines.Select(d => d.Name).Should().Equal("Electrical", "Structural");
        summary.Authors.Select(a => a.Name).Should().Equal("AFCO", "JINGGONG");
        summary.Authors.Single(a => a.Name == "JINGGONG").Total.Should().Be(2);
        summary.Total.Total.Should().Be(3);
        summary.Total.Name.Should().Be(CorporateSummaryEngine.TotalRowName);
    }

    // The sheet counts per author name, so a document with no Exchange 01 author
    // appears in no author row — but still counts in its discipline and the totals.
    [Fact]
    public void DocumentsWithoutAnAuthor_AreExcludedFromTheAuthorBreakdownOnly()
    {
        var summary = Compute(
            new Row("Structural", Author: "AFCO"),
            new Row("Structural", Author: null),
            new Row("Structural", Author: "   "));

        summary.Authors.Should().ContainSingle().Which.Name.Should().Be("AFCO");
        summary.Disciplines.Single().Total.Should().Be(3);
        summary.Total.Total.Should().Be(3);
    }

    [Fact]
    public void DocumentWithNoTrackerRow_StillCountsAsPending()
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            DocumentNumber = "DOC-0001",
            CorporateDiscipline = "Landscape",
        };

        var summary = CorporateSummaryEngine.Compute(
            new[] { document }, Array.Empty<TrackerRow>(), Project());

        var group = summary.Disciplines.Single();
        group.Total.Should().Be(1);
        group.PvPending.Should().Be(1);
        group.EvPending.Should().Be(1);
        group.PlannedPercent.Should().Be(0m);
    }

    [Fact]
    public void ReportDateOverride_WinsOverTheProjectSetting()
    {
        var document = new Document { Id = Guid.NewGuid(), CorporateDiscipline = "Structural" };
        var row = new TrackerRow(
            document.Id, "DOC-0001", null, null, null, null, null, null, null,
            PlannedStart: new DateTime(2026, 12, 1), PlannedFinish: null,
            ActualStart: null, ActualFinish: null);

        var summary = CorporateSummaryEngine.Compute(
            new[] { document }, new[] { row }, Project(), new DateTime(2027, 1, 3));

        summary.CurrentWeek.Should().Be(new DateTime(2027, 1, 3, 23, 59, 59));
        summary.Disciplines.Single().Planned.Should().Be(1,
            "the later report date brings the planned start into range");
    }
}
