using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Dip.Infrastructure.Tests;

// PLAN.md § 9 (المرحلة 5 / 5.5): with every document weighted 1, Structural's SPI is
// ≈ 0.92 and Electrical's ≈ 3.1 at the sample report date (2026-08-30).
//
// Those two numbers are the Corporate Summary's own ratios: Structural
// Completed% 0.5939 / Planned% 0.6445 = 0.9215, Electrical 0.2591 / 0.0833 = 3.11.
// This test asserts the engine reproduces them from the same workbook data, and that
// SPI really is EarnedPercent / PlannedPercent for every discipline.
public class EvmSampleTests
{
    private static readonly DateTime ReportDate = new(2026, 8, 30);

    private readonly ITestOutputHelper _output;

    public EvmSampleTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void SpiPerDiscipline_MatchesTheCorporateSummaryRatios()
    {
        using var workbook = TrackerWorkbook.Open();

        var sheetRows = TrackerWorkbook.ReadTrackerSheet(workbook);
        var documents = sheetRows.Select(TrackerWorkbook.ToDocument).ToList();
        var revisions = TrackerWorkbook.ReadAconexHistory(workbook);
        var baseline = TrackerWorkbook.ReadBaseline(workbook);

        // Every document weighted 1, which is what the PLAN targets assume.
        documents.Should().OnlyContain(d => d.BudgetWeight == 1m);

        var project = new Project { ScheduleMode = ScheduleMode.Baseline, ReportDate = ReportDate };
        var trackerRows = TrackerEngine.Compute(
            documents, revisions, baseline, TrackerWorkbook.SeededStatusMappings(), project);

        var evm = EvmEngine.Compute(documents, trackerRows, project);
        var corporate = CorporateSummaryEngine.Compute(documents, trackerRows, project);

        foreach (var row in evm.Disciplines)
        {
            _output.WriteLine(
                $"{row.Name,-20} PV={row.PlannedValue,9:0.0} EV={row.EarnedValue,9:0.0} " +
                $"BAC={row.BudgetAtCompletion,7:0} SPI={row.SchedulePerformanceIndex:0.0000}");
        }

        var structural = evm.Disciplines.Single(d => d.Name == "Structural");
        structural.SchedulePerformanceIndex.Should().BeApproximately(0.92m, 0.01m);

        var electrical = evm.Disciplines.Single(d => d.Name == "Electrical");
        electrical.SchedulePerformanceIndex.Should().BeApproximately(3.1m, 0.05m);

        // With weight 1 the budget is just the document count, and PV% is exactly the
        // Corporate Summary's Planned% — both ask "planned to have started/finished by
        // the report date".
        //
        // EV% is NOT always the Corporate Summary's Completed%. The summary credits a
        // document whose unified status is Approved; EVM credits it only once it was
        // approved ON OR BEFORE the report date (PLAN.md § 7: "كل Actual يُفلتر ≤ R").
        // A document approved after the report date therefore earns its submission
        // credit here and full credit there.
        var lateApprovals = trackerRows
            .Where(r => r.ActualFinish is not null && r.ActualFinish > ReportDate)
            .Select(r => r.DocumentId)
            .ToHashSet();
        var lateByDiscipline = documents
            .Where(d => lateApprovals.Contains(d.Id))
            .GroupBy(d => d.CorporateDiscipline, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        _output.WriteLine($"approved after the report date: {lateApprovals.Count} documents");

        foreach (var group in corporate.Disciplines)
        {
            var row = evm.Disciplines.Single(d => d.Name == group.Name);
            row.BudgetAtCompletion.Should().Be(group.Total);
            row.PlannedPercent.Should().BeApproximately(group.PlannedPercent, 0.0001m,
                "PV% is the Corporate Summary's Planned% for {0}", group.Name);

            if (lateByDiscipline.ContainsKey(group.Name))
            {
                row.EarnedPercent.Should().BeLessThan(group.CompletedPercent,
                    "{0} has {1} document(s) approved after the report date, which EVM does not credit",
                    group.Name, lateByDiscipline[group.Name]);
            }
            else
            {
                row.EarnedPercent.Should().BeApproximately(group.CompletedPercent, 0.0001m,
                    "with no late approvals, EV% is the Corporate Summary's Completed% for {0}", group.Name);
            }

            if (row.PlannedValue > 0)
            {
                row.SchedulePerformanceIndex.Should().BeApproximately(
                    row.EarnedValue / row.PlannedValue, 0.0001m);
            }
        }

        lateApprovals.Should().NotBeEmpty(
            "the sample contains approvals after 2026-08-30, which is what makes the two "
            + "definitions distinguishable at all");

        evm.Total.BudgetAtCompletion.Should().Be(corporate.Total.Total);
        evm.Total.Documents.Should().Be(15_883);
    }
}
