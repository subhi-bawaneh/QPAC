using Dip.Application.Abstractions;
using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Dip.Infrastructure.Tests;

// PLAN.md § 9 (المرحلة 5 / 5.2): reproduce Tracker.xlsx!'Corporate Summary' at
// ReportDate 2026-08-30 — 9 discipline rows, 13 author rows, the totals row and
// every weekly bucket.
//
// Expected values are read from the sheet's cached cells rather than typed in
// (CLAUDE.md hard rule 8); docs/excel-analysis.md § 4.4 lists the same numbers.
//
// Sheet layout: C4 Report Date, C5 Current Week, G4 Start Week, G5 End Week,
// row 5 totals, row 7 headers, weekly rows from row 8 down column B, discipline
// rows from row 8 down column I, an "Authors" label row, then the author rows.
public class CorporateSummarySampleTests
{
    private const decimal PercentTolerance = 0.0001m;

    private readonly ITestOutputHelper _output;

    public CorporateSummarySampleTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ReproducesTheCorporateSummarySheet()
    {
        using var workbook = TrackerWorkbook.Open();

        var sheetRows = TrackerWorkbook.ReadTrackerSheet(workbook);
        var documents = sheetRows.Select(TrackerWorkbook.ToDocument).ToList();
        var revisions = TrackerWorkbook.ReadAconexHistory(workbook);
        var baseline = TrackerWorkbook.ReadBaseline(workbook);

        var expected = ReadSummarySheet(workbook);

        // Anchors from docs/excel-analysis.md § 4.4: if the reader ever mapped the wrong
        // columns, the comparison below would still "pass" against whatever it read.
        expected.ReportDate.Date.Should().Be(new DateTime(2026, 8, 30));
        expected.Total.Total.Should().Be(15_883);
        expected.Total.Planned.Should().Be(4_493);
        expected.Total.Submitted.Should().Be(5_531);
        expected.Total.Approved.Should().Be(3_834);
        expected.Total.TotalRevisions.Should().Be(8_086);

        var electrical = expected.Disciplines.Single(d => d.Name == "Electrical");
        electrical.Total.Should().Be(2_998);
        electrical.Planned.Should().Be(252);
        electrical.Submitted.Should().Be(858);
        electrical.Approved.Should().Be(624);
        electrical.Quality.Should().BeApproximately(0.6190m, 0.0001m);
        electrical.PlannedPercent.Should().BeApproximately(0.0833m, 0.0001m);
        electrical.CompletedPercent.Should().BeApproximately(0.2591m, 0.0001m);

        var structural = expected.Disciplines.Single(d => d.Name == "Structural");
        structural.Total.Should().Be(5_277);
        structural.PlannedPercent.Should().BeApproximately(0.6445m, 0.0001m);
        structural.CompletedPercent.Should().BeApproximately(0.5939m, 0.0001m);

        var project = new Project
        {
            ScheduleMode = ScheduleMode.Baseline,
            ReportDate = expected.ReportDate,
        };

        var trackerRows = TrackerEngine.Compute(
            documents, revisions, baseline, TrackerWorkbook.SeededStatusMappings(), project);
        var summary = CorporateSummaryEngine.Compute(documents, trackerRows, project);

        var failures = new List<string>();

        // ---- header
        CheckDate(failures, "Current Week", expected.CurrentWeek, summary.CurrentWeek);
        CheckDate(failures, "Start Week", expected.StartWeek, summary.StartWeek);
        CheckDate(failures, "End Week", expected.EndWeek, summary.EndWeek);

        // ---- totals row
        CompareGroup(failures, "Total", expected.Total, summary.Total);

        // ---- disciplines and authors
        expected.Disciplines.Should().HaveCount(9, "the sheet lists 9 corporate disciplines");
        expected.Authors.Should().HaveCount(13, "the sheet lists 13 authors");

        CompareGroups(failures, "Discipline", expected.Disciplines, summary.Disciplines);
        CompareGroups(failures, "Author", expected.Authors, summary.Authors);

        // ---- weekly buckets
        expected.Weeks.Should().HaveCountGreaterThan(140, "the timeline runs to 2028");
        summary.Weeks.Should().HaveCount(expected.Weeks.Count, "the timeline must span the same weeks");

        foreach (var week in expected.Weeks)
        {
            var actual = summary.Weeks.SingleOrDefault(w => w.Number == week.Number);
            if (actual is null)
            {
                failures.Add($"Week {week.Number}: missing from the computed timeline");
                continue;
            }

            CheckDate(failures, $"Week {week.Number} From", week.From, actual.From);
            CheckDate(failures, $"Week {week.Number} To", week.To, actual.To);
            Check(failures, $"Week {week.Number} Planned", week.Planned, actual.Planned);
            Check(failures, $"Week {week.Number} Submitted", week.Submitted, actual.Submitted);
            Check(failures, $"Week {week.Number} Approved", week.Approved, actual.Approved);
        }

        foreach (var failure in failures.Take(30)) _output.WriteLine(failure);

        failures.Should().BeEmpty(
            "the engine must reproduce the Corporate Summary sheet ({0} differences)", failures.Count);
    }

    private static void CompareGroups(
        List<string> failures, string kind,
        IReadOnlyList<ExpectedGroup> expected, IReadOnlyList<SummaryGroup> actual)
    {
        foreach (var group in expected)
        {
            var computed = actual.SingleOrDefault(g => g.Name == group.Name);
            if (computed is null)
            {
                failures.Add($"{kind} '{group.Name}': missing from the computed summary");
                continue;
            }
            CompareGroup(failures, group.Name, group, computed);
        }

        // The engine must not invent groups the sheet doesn't have.
        foreach (var extra in actual.Where(a => expected.All(e => e.Name != a.Name)))
        {
            failures.Add($"{kind} '{extra.Name}': computed but absent from the sheet ({extra.Total} documents)");
        }
    }

    private static void CompareGroup(
        List<string> failures, string name, ExpectedGroup expected, SummaryGroup actual)
    {
        Check(failures, $"{name} Total", expected.Total, actual.Total);
        Check(failures, $"{name} Planned", expected.Planned, actual.Planned);
        Check(failures, $"{name} Submitted", expected.Submitted, actual.Submitted);
        Check(failures, $"{name} Approved", expected.Approved, actual.Approved);

        Check(failures, $"{name} Quality/Approved", expected.QualityApproved, actual.QualityApproved);
        Check(failures, $"{name} Rejected", expected.Rejected, actual.Rejected);
        Check(failures, $"{name} UnderReview", expected.UnderReview, actual.UnderReview);
        Check(failures, $"{name} Withdrawn", expected.Withdrawn, actual.Withdrawn);
        Check(failures, $"{name} TotalRevisions", expected.TotalRevisions, actual.TotalRevisions);
        CheckPercent(failures, $"{name} Quality", expected.Quality, actual.Quality);

        Check(failures, $"{name} PV Pending", expected.PvPending, actual.PvPending);
        Check(failures, $"{name} PV Sub1", expected.PvSub1, actual.PvSub1);
        Check(failures, $"{name} PV Sub2", expected.PvSub2, actual.PvSub2);
        Check(failures, $"{name} PV Approved", expected.PvApproved, actual.PvApproved);
        CheckPercent(failures, $"{name} Planned%", expected.PlannedPercent, actual.PlannedPercent);

        Check(failures, $"{name} EV Pending", expected.EvPending, actual.EvPending);
        Check(failures, $"{name} EV Sub1", expected.EvSub1, actual.EvSub1);
        Check(failures, $"{name} EV Sub2", expected.EvSub2, actual.EvSub2);
        Check(failures, $"{name} EV Approved", expected.EvApproved, actual.EvApproved);
        CheckPercent(failures, $"{name} Completed%", expected.CompletedPercent, actual.CompletedPercent);
    }

    private static void Check(List<string> failures, string label, int expected, int actual)
    {
        if (expected != actual) failures.Add($"{label}: expected {expected}, got {actual}");
    }

    private static void CheckPercent(List<string> failures, string label, decimal? expected, decimal? actual)
    {
        var ok = expected is null
            ? actual is null
            : actual is not null && Math.Abs(expected.Value - actual.Value) <= PercentTolerance;
        if (!ok)
        {
            failures.Add($"{label}: expected {Show(expected)}, got {Show(actual)}");
        }
    }

    private static void CheckDate(List<string> failures, string label, DateTime expected, DateTime actual)
    {
        // The sheet stores week boundaries as dates; the engine carries 23:59:59.
        if (expected.Date != actual.Date)
        {
            failures.Add($"{label}: expected {expected:yyyy-MM-dd}, got {actual:yyyy-MM-dd}");
        }
    }

    private static string Show(decimal? value) =>
        value is null ? "<null>" : value.Value.ToString("0.####");

    // ------------------------------------------------------------------ sheet

    private static ExpectedSummary ReadSummarySheet(IExcelWorkbook workbook)
    {
        var sheet = workbook.Sheet("Corporate Summary");
        var headerRow = TrackerWorkbook.HeaderRow(sheet, "Total Drawings");
        var firstRow = headerRow + 1;

        var colWeek = TrackerWorkbook.Column(sheet, headerRow, "Week");
        var colFrom = TrackerWorkbook.Column(sheet, headerRow, "From");
        var colTo = TrackerWorkbook.Column(sheet, headerRow, "To");
        var colWeeklyPlanned = TrackerWorkbook.Column(sheet, headerRow, "Weekly Planned");
        var colWeeklySubmitted = TrackerWorkbook.Column(sheet, headerRow, "Weekly Submitted");
        var colWeeklyApproved = TrackerWorkbook.Column(sheet, headerRow, "Weekly Approved");

        // Group name columns repeat once per panel; the panels are in a fixed order.
        var groupNameColumns = Enumerable.Range(1, sheet.ColumnCount)
            .Where(c => string.Equals(sheet.Row(headerRow).Cell(c).GetStringOrNull()?.Trim(),
                "Disciplines", StringComparison.OrdinalIgnoreCase))
            .ToList();
        groupNameColumns.Should().HaveCount(4, "Progress, Quality, Planned Value and Earned Value each repeat the name");

        var progress = groupNameColumns[0];
        var quality = groupNameColumns[1];
        var plannedValue = groupNameColumns[2];
        var earnedValue = groupNameColumns[3];

        var weeks = new List<ExpectedWeek>();
        for (var r = firstRow; r <= sheet.RowCount; r++)
        {
            var row = sheet.Row(r);
            var label = row.Cell(colWeek).GetStringOrNull()?.Trim();
            if (string.IsNullOrEmpty(label) || !label.StartsWith("Week ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var from = row.Cell(colFrom).GetDateTime();
            var to = row.Cell(colTo).GetDateTime();
            if (from is null || to is null) continue;

            weeks.Add(new ExpectedWeek(
                Number: int.Parse(label[5..].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                From: from.Value,
                To: to.Value,
                Planned: row.Cell(colWeeklyPlanned).GetInt() ?? 0,
                Submitted: row.Cell(colWeeklySubmitted).GetInt() ?? 0,
                Approved: row.Cell(colWeeklyApproved).GetInt() ?? 0));
        }

        // Disciplines run until the "Authors" label; authors until the names stop.
        var disciplines = new List<ExpectedGroup>();
        var authors = new List<ExpectedGroup>();
        var target = disciplines;
        for (var r = firstRow; r <= sheet.RowCount; r++)
        {
            var name = sheet.Row(r).Cell(progress).GetStringOrNull()?.Trim();
            if (string.IsNullOrEmpty(name) || name == "0") continue;
            if (string.Equals(name, "Authors", StringComparison.OrdinalIgnoreCase))
            {
                target = authors;
                continue;
            }
            target.Add(ReadGroup(sheet, r, name, progress, quality, plannedValue, earnedValue));
        }

        var totalsRow = TrackerWorkbook.HeaderRow(sheet, "Current Week");
        var total = ReadGroup(sheet, totalsRow, "Total", progress, quality, plannedValue, earnedValue);

        return new ExpectedSummary(
            ReportDate: DateCell(sheet, TrackerWorkbook.HeaderRow(sheet, "Report Date"), "Report Date"),
            CurrentWeek: DateCell(sheet, totalsRow, "Current Week"),
            StartWeek: DateCell(sheet, TrackerWorkbook.HeaderRow(sheet, "Start Week"), "Start Week"),
            EndWeek: DateCell(sheet, TrackerWorkbook.HeaderRow(sheet, "End Week"), "End Week"),
            Weeks: weeks,
            Disciplines: disciplines,
            Authors: authors,
            Total: total);
    }

    // Header values sit in the cell immediately right of their label.
    private static DateTime DateCell(IExcelSheet sheet, int rowNumber, string label)
    {
        var row = sheet.Row(rowNumber);
        for (var c = 1; c < sheet.ColumnCount; c++)
        {
            if (string.Equals(row.Cell(c).GetStringOrNull()?.Trim(), label, StringComparison.OrdinalIgnoreCase))
            {
                return row.Cell(c + 1).GetDateTime()
                    ?? throw new InvalidOperationException($"'{label}' has no date value");
            }
        }
        throw new InvalidOperationException($"Corporate Summary has no '{label}' label");
    }

    private static ExpectedGroup ReadGroup(
        IExcelSheet sheet, int rowNumber, string name,
        int progress, int quality, int plannedValue, int earnedValue)
    {
        var row = sheet.Row(rowNumber);
        int Number(int column) => row.Cell(column).GetInt() ?? 0;
        decimal? Ratio(int column) => row.Cell(column).GetDecimal();

        return new ExpectedGroup(
            Name: name,
            Total: Number(progress + 1),
            Planned: Number(progress + 2),
            Submitted: Number(progress + 3),
            Approved: Number(progress + 4),
            QualityApproved: Number(quality + 1),
            Rejected: Number(quality + 2),
            UnderReview: Number(quality + 3),
            Withdrawn: Number(quality + 4),
            TotalRevisions: Number(quality + 5),
            Quality: Ratio(quality + 6),
            PvPending: Number(plannedValue + 1),
            PvSub1: Number(plannedValue + 2),
            PvSub2: Number(plannedValue + 3),
            PvApproved: Number(plannedValue + 4),
            PlannedPercent: Ratio(plannedValue + 5) ?? 0m,
            EvPending: Number(earnedValue + 1),
            EvSub1: Number(earnedValue + 2),
            EvSub2: Number(earnedValue + 3),
            EvApproved: Number(earnedValue + 4),
            CompletedPercent: Ratio(earnedValue + 5) ?? 0m);
    }

    private sealed record ExpectedSummary(
        DateTime ReportDate,
        DateTime CurrentWeek,
        DateTime StartWeek,
        DateTime EndWeek,
        IReadOnlyList<ExpectedWeek> Weeks,
        IReadOnlyList<ExpectedGroup> Disciplines,
        IReadOnlyList<ExpectedGroup> Authors,
        ExpectedGroup Total);

    private sealed record ExpectedWeek(
        int Number, DateTime From, DateTime To, int Planned, int Submitted, int Approved);

    private sealed record ExpectedGroup(
        string Name,
        int Total, int Planned, int Submitted, int Approved,
        int QualityApproved, int Rejected, int UnderReview, int Withdrawn,
        int TotalRevisions, decimal? Quality,
        int PvPending, int PvSub1, int PvSub2, int PvApproved, decimal PlannedPercent,
        int EvPending, int EvSub1, int EvSub2, int EvApproved, decimal CompletedPercent);
}
