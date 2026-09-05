using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

public class EvmEngineTests
{
    private static readonly DateTime ReportDate = new(2026, 8, 30);

    private static Project Project() => new() { ReportDate = ReportDate };

    private sealed record Row(
        string Discipline = "Structural",
        decimal Weight = 1m,
        DateTime? PlannedStart = null,
        DateTime? PlannedFinish = null,
        DateTime? ActualFinish = null,
        int? Submissions = null);

    private static EvmSummary Compute(params Row[] rows)
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
                BudgetWeight = row.Weight,
            };
            documents.Add(document);
            trackerRows.Add(new TrackerRow(
                document.Id, document.DocumentNumber,
                SubmissionsCount: row.Submissions, Revision: null, AconexStatus: null,
                Status: null, SubmissionDate: null, DateModified: null, Transmittal: null,
                PlannedStart: row.PlannedStart, PlannedFinish: row.PlannedFinish,
                ActualStart: null, ActualFinish: row.ActualFinish));
        }

        return EvmEngine.Compute(documents, trackerRows, Project());
    }

    private static readonly DateTime Before = new(2026, 1, 1);
    private static readonly DateTime After = new(2027, 1, 1);

    [Fact]
    public void PlannedValue_CreditsFullWeightOnceThePlannedFinishHasPassed()
    {
        var row = Compute(new Row(PlannedStart: Before, PlannedFinish: Before)).Total;

        row.PlannedValue.Should().Be(1.0m);
    }

    [Fact]
    public void PlannedValue_CreditsTheSub1WeightOnceThePlannedStartHasPassed()
    {
        var row = Compute(new Row(PlannedStart: Before, PlannedFinish: After)).Total;

        row.PlannedValue.Should().Be(0.6m);
    }

    [Fact]
    public void PlannedValue_CreditsNothingBeforeThePlannedStart()
    {
        var row = Compute(new Row(PlannedStart: After, PlannedFinish: After)).Total;

        row.PlannedValue.Should().Be(0m);
    }

    [Fact]
    public void EarnedValue_CreditsFullWeightForAnApprovalOnOrBeforeTheReportDate()
    {
        var row = Compute(new Row(ActualFinish: Before, Submissions: 2)).Total;

        row.EarnedValue.Should().Be(1.0m);
    }

    // PLAN.md § 7 filters every actual to the report date, so an approval that
    // happened later has not been earned yet — it only keeps its submission credit.
    [Fact]
    public void EarnedValue_DoesNotCreditAnApprovalAfterTheReportDate()
    {
        var row = Compute(new Row(ActualFinish: After, Submissions: 2)).Total;

        row.EarnedValue.Should().Be(0.9m);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(1, 0.6)]
    [InlineData(2, 0.9)]
    [InlineData(5, 0.9)]
    public void EarnedValue_FallsBackToTheSubmissionCount(int? submissions, double expected)
    {
        var row = Compute(new Row(Submissions: submissions)).Total;

        row.EarnedValue.Should().Be((decimal)expected);
    }

    [Fact]
    public void Spi_IsEarnedOverPlanned_AndVarianceIsTheirDifference()
    {
        var summary = Compute(
            new Row(PlannedStart: Before, PlannedFinish: Before, ActualFinish: Before),
            new Row(PlannedStart: Before, PlannedFinish: Before, Submissions: 1));

        var row = summary.Total;
        row.PlannedValue.Should().Be(2.0m);
        row.EarnedValue.Should().Be(1.6m);
        row.SchedulePerformanceIndex.Should().Be(0.8m);
        row.ScheduleVariance.Should().Be(-0.4m);
    }

    [Fact]
    public void Spi_IsNullWhenNothingIsPlannedYet()
    {
        var row = Compute(new Row(PlannedStart: After)).Total;

        row.PlannedValue.Should().Be(0m);
        row.SchedulePerformanceIndex.Should().BeNull();
        row.PlannedPercent.Should().Be(0m);
    }

    // The weight is the document's budget: a 10-day exchange counts ten times a
    // 1-day one, so SPI is duration-weighted rather than a document count.
    [Fact]
    public void Weights_ScaleEachDocumentsContribution()
    {
        var summary = Compute(
            new Row(Weight: 10m, PlannedStart: Before, PlannedFinish: Before, ActualFinish: Before),
            new Row(Weight: 1m, PlannedStart: Before, PlannedFinish: Before));

        var row = summary.Total;
        row.BudgetAtCompletion.Should().Be(11m);
        row.PlannedValue.Should().Be(11m);
        row.EarnedValue.Should().Be(10m);
        row.EarnedPercent.Should().BeApproximately(10m / 11m, 0.0001m);
    }

    [Fact]
    public void ProjectWeights_AreConfigurable()
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            CorporateDiscipline = "Structural",
            BudgetWeight = 1m,
        };
        var row = new TrackerRow(
            document.Id, "DOC-0001", SubmissionsCount: 1, Revision: null, AconexStatus: null,
            Status: null, SubmissionDate: null, DateModified: null, Transmittal: null,
            PlannedStart: Before, PlannedFinish: After, ActualStart: null, ActualFinish: null);

        var project = new Project { ReportDate = ReportDate, WeightSub1 = 0.5m };

        var summary = EvmEngine.Compute(new[] { document }, new[] { row }, project);

        summary.Total.PlannedValue.Should().Be(0.5m);
        summary.Total.EarnedValue.Should().Be(0.5m);
    }

    [Fact]
    public void Disciplines_AreReportedSeparatelyAndSumToTheTotal()
    {
        var summary = Compute(
            new Row("Structural", PlannedStart: Before, PlannedFinish: Before, ActualFinish: Before),
            new Row("Electrical", PlannedStart: Before, PlannedFinish: After, Submissions: 1),
            new Row("Electrical"));

        summary.Disciplines.Select(d => d.Name).Should().Equal("Electrical", "Structural");
        summary.Disciplines.Sum(d => d.PlannedValue).Should().Be(summary.Total.PlannedValue);
        summary.Disciplines.Sum(d => d.EarnedValue).Should().Be(summary.Total.EarnedValue);
        summary.Total.Documents.Should().Be(3);
    }

    [Fact]
    public void Cpi_IsAlwaysNullUntilActualHoursExist()
    {
        Compute(new Row()).Total.CostPerformanceIndex.Should().BeNull();
    }

    [Fact]
    public void DocumentWithNoTrackerRow_CountsTowardsTheBudgetOnly()
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            CorporateDiscipline = "Landscape",
            BudgetWeight = 3m,
        };

        var summary = EvmEngine.Compute(new[] { document }, Array.Empty<TrackerRow>(), Project());

        summary.Total.BudgetAtCompletion.Should().Be(3m);
        summary.Total.PlannedValue.Should().Be(0m);
        summary.Total.EarnedValue.Should().Be(0m);
    }
}
