using Dip.Application.Abstractions;
using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Dip.Infrastructure.Tests;

// PLAN.md § 9 (المرحلة 5 / 5.3): reproduce Tracker.xlsx!'Baseline Summary' —
// the discipline rows, every package row, and the packages-by-status rollup.
//
// Expected values are read from the sheet's cached cells (CLAUDE.md hard rule 8).
//
// Sheet layout: row 2 project totals, row 4 discipline headers, rows 5-13 the nine
// disciplines, row 15 package headers, rows 16+ one row per Submittal activity,
// and a Packages Summary block in columns L-N.
//
// Exception: the sheet's own `C-Revise and Resubmit` and `D-Rejected` columns read 0
// in every row. Their formula compares MIDP[Aconex Status] against the header text,
// which drops the spaces around the dash ("C-Revise..." vs the data's "C - Revise..."),
// so the COUNTIFS never matches. Those two columns are asserted against the real
// status counts instead — docs/excel-analysis.md § 6, finding 20.
public class BaselineSummarySampleTests
{
    private readonly ITestOutputHelper _output;

    public BaselineSummarySampleTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ReproducesTheBaselineSummarySheet()
    {
        using var workbook = TrackerWorkbook.Open();

        var sheetRows = TrackerWorkbook.ReadTrackerSheet(workbook);
        var documents = sheetRows.Select(TrackerWorkbook.ToDocument).ToList();
        var revisions = TrackerWorkbook.ReadAconexHistory(workbook);
        var baseline = TrackerWorkbook.ReadBaseline(workbook);

        var project = new Project { ScheduleMode = ScheduleMode.Baseline };
        var trackerRows = TrackerEngine.Compute(
            documents, revisions, baseline, TrackerWorkbook.SeededStatusMappings(), project);

        var summary = BaselineSummaryEngine.Compute(documents, trackerRows, baseline);
        var expected = ReadSummarySheet(workbook);

        // Anchors from docs/excel-analysis.md § 4.5 and PLAN.md § 9.
        expected.Total.Total.Should().Be(15_883);
        expected.Total.Submitted.Should().Be(5_531);
        expected.Total.Approved.Should().Be(3_834);
        expected.Packages.Should().HaveCount(683, "the sheet lists 683 Submittal packages");
        expected.Packages.Single(p => p.ActivityCode == "QP.C.PS.GEN.GEN.1810")
            .Status.Should().Be(PackageStatus.Unused);
        var pending1850 = expected.Packages.Single(p => p.ActivityCode == "QP.C.PS.GEN.GEN.1850");
        pending1850.Total.Should().Be(100);
        pending1850.Status.Should().Be(PackageStatus.Pending);

        var failures = new List<string>();

        // ---- project totals and disciplines
        CompareCounts(failures, "Total", expected.Total, summary.Total);

        expected.Disciplines.Should().HaveCount(9);
        foreach (var discipline in expected.Disciplines)
        {
            var actual = summary.Disciplines.SingleOrDefault(d => d.Name == discipline.Name);
            if (actual is null)
            {
                failures.Add($"Discipline '{discipline.Name}': missing from the computed summary");
                continue;
            }
            CompareCounts(failures, discipline.Name, discipline, actual);
        }

        // ---- one row per Submittal package
        summary.Packages.Should().HaveCount(expected.Packages.Count,
            "every Submittal activity gets exactly one package row");

        foreach (var package in expected.Packages)
        {
            var actual = summary.Packages.SingleOrDefault(p => p.ActivityCode == package.ActivityCode);
            if (actual is null)
            {
                failures.Add($"Package '{package.ActivityCode}': missing from the computed summary");
                continue;
            }

            Check(failures, $"{package.ActivityCode} Total", package.Total, actual.Total);
            Check(failures, $"{package.ActivityCode} Submitted", package.Submitted, actual.Submitted);
            Check(failures, $"{package.ActivityCode} Approved", package.Approved, actual.Approved);
            Check(failures, $"{package.ActivityCode} UnderReview", package.UnderReview, actual.UnderReview);
            if (package.Status != actual.Status)
            {
                failures.Add($"{package.ActivityCode} Status: expected {package.Status}, got {actual.Status}");
            }
        }

        // ---- packages-by-status rollup
        foreach (var status in expected.PackageStatuses)
        {
            var actual = summary.PackageStatuses.Single(s => s.Status == status.Status);
            Check(failures, $"{status.Status} packages", status.Packages, actual.Packages);
            Check(failures, $"{status.Status} drawings", status.Drawings, actual.Drawings);
        }

        Check(failures, "Total packages", expected.TotalPackages, summary.TotalPackages);
        Check(failures, "Total package drawings", expected.TotalPackageDrawings, summary.TotalPackageDrawings);

        foreach (var failure in failures.Take(30)) _output.WriteLine(failure);
        failures.Should().BeEmpty(
            "the engine must reproduce the Baseline Summary sheet ({0} differences)", failures.Count);
    }

    // The sheet's C-Revise / D-Rejected columns are always 0 because of the header
    // mismatch described above; the engine counts the real statuses instead.
    [Fact]
    public void CReviseAndDRejected_CountTheRealStatuses_WhereTheSheetReadsZero()
    {
        using var workbook = TrackerWorkbook.Open();

        var sheetRows = TrackerWorkbook.ReadTrackerSheet(workbook);
        var documents = sheetRows.Select(TrackerWorkbook.ToDocument).ToList();
        var revisions = TrackerWorkbook.ReadAconexHistory(workbook);
        var baseline = TrackerWorkbook.ReadBaseline(workbook);

        var project = new Project { ScheduleMode = ScheduleMode.Baseline };
        var trackerRows = TrackerEngine.Compute(
            documents, revisions, baseline, TrackerWorkbook.SeededStatusMappings(), project);
        var summary = BaselineSummaryEngine.Compute(documents, trackerRows, baseline);

        var expected = ReadSummarySheet(workbook);
        expected.Total.CRevise.Should().Be(0, "the sheet's own column is broken");
        expected.Total.DRejected.Should().Be(0, "the sheet's own column is broken");

        // Counted straight from the Tracker sheet's Aconex Status column.
        var cRevise = sheetRows.Count(r => r.AconexStatus == BaselineSummaryEngine.CReviseStatus);
        var dRejected = sheetRows.Count(r => r.AconexStatus == BaselineSummaryEngine.DRejectedStatus);

        cRevise.Should().BeGreaterThan(0, "the sample data contains revise-and-resubmit documents");
        summary.Total.CRevise.Should().Be(cRevise);
        summary.Total.DRejected.Should().Be(dRejected);

        // And they add up to the Rejected count the Corporate Summary reports.
        var corporate = CorporateSummaryEngine.Compute(
            documents, trackerRows, new Project { ScheduleMode = ScheduleMode.Baseline },
            new DateTime(2026, 8, 30));
        (summary.Total.CRevise + summary.Total.DRejected).Should().Be(corporate.Total.Rejected);
    }

    private static void CompareCounts(
        List<string> failures, string name, ExpectedRow expected, BaselineDisciplineRow actual)
    {
        Check(failures, $"{name} Total", expected.Total, actual.Total);
        Check(failures, $"{name} Submitted", expected.Submitted, actual.Submitted);
        Check(failures, $"{name} Approved", expected.Approved, actual.Approved);
        Check(failures, $"{name} UnderReview", expected.UnderReview, actual.UnderReview);
    }

    private static void Check(List<string> failures, string label, int expected, int actual)
    {
        if (expected != actual) failures.Add($"{label}: expected {expected}, got {actual}");
    }

    // ------------------------------------------------------------------ sheet

    private static ExpectedSummary ReadSummarySheet(IExcelWorkbook workbook)
    {
        var sheet = workbook.Sheet("Baseline Summary");

        var disciplineHeader = TrackerWorkbook.HeaderRow(sheet, "Total Drawings");
        var colName = TrackerWorkbook.Column(sheet, disciplineHeader, "Disciplines");
        var colTotal = TrackerWorkbook.Column(sheet, disciplineHeader, "Total Drawings");
        var colSubmitted = TrackerWorkbook.Column(sheet, disciplineHeader, "Submitted");
        var colApproved = TrackerWorkbook.Column(sheet, disciplineHeader, "Approved");
        var colCRevise = TrackerWorkbook.Column(sheet, disciplineHeader, "C-Revise and Resubmit");
        var colDRejected = TrackerWorkbook.Column(sheet, disciplineHeader, "D-Rejected");
        var colUnderReview = TrackerWorkbook.Column(sheet, disciplineHeader, "Under Review");

        // The project totals sit one row above the discipline header, under "Total".
        var totalsRow = TrackerWorkbook.HeaderRow(sheet, "Baseline Progress");
        var total = ReadRow(sheet, totalsRow, "Total",
            colTotal, colSubmitted, colApproved, colCRevise, colDRejected, colUnderReview);

        var disciplines = new List<ExpectedRow>();
        for (var r = disciplineHeader + 1; r <= sheet.RowCount; r++)
        {
            var name = TrackerWorkbook.Text(sheet.Row(r).Cell(colName));
            if (string.IsNullOrEmpty(name)) break;
            disciplines.Add(ReadRow(sheet, r, name,
                colTotal, colSubmitted, colApproved, colCRevise, colDRejected, colUnderReview));
        }

        // Package block: same count columns, keyed by Activity Code.
        var packageHeader = TrackerWorkbook.HeaderRow(sheet, "Activity Code");
        var colActivityCode = TrackerWorkbook.Column(sheet, packageHeader, "Activity Code");
        var colPackage = TrackerWorkbook.Column(sheet, packageHeader, "Package");
        var colStatus = TrackerWorkbook.Column(sheet, packageHeader, "Status");

        var packages = new List<ExpectedPackage>();
        for (var r = packageHeader + 1; r <= sheet.RowCount; r++)
        {
            var row = sheet.Row(r);
            var code = TrackerWorkbook.Text(row.Cell(colActivityCode));
            if (string.IsNullOrEmpty(code)) continue;

            var counts = ReadRow(sheet, r, code,
                colTotal, colSubmitted, colApproved, colCRevise, colDRejected, colUnderReview);

            packages.Add(new ExpectedPackage(
                ActivityCode: code,
                Package: TrackerWorkbook.Text(row.Cell(colPackage)) ?? string.Empty,
                Total: counts.Total,
                Submitted: counts.Submitted,
                Approved: counts.Approved,
                UnderReview: counts.UnderReview,
                Status: Enum.Parse<PackageStatus>(
                    TrackerWorkbook.Text(row.Cell(colStatus))
                    ?? throw new InvalidOperationException($"Package {code} has no Status"), true)));
        }

        // Packages Summary block (L-N): one row per status plus a Total row.
        var summaryHeader = TrackerWorkbook.HeaderRow(sheet, "No of Packages");
        var colSummaryStatus = TrackerWorkbook.Column(sheet, summaryHeader, "Status");
        var colPackageCount = TrackerWorkbook.Column(sheet, summaryHeader, "No of Packages");
        var colDrawingCount = TrackerWorkbook.Column(sheet, summaryHeader, "No of Drawings");

        var statuses = new List<PackageStatusCount>();
        var totalPackages = 0;
        var totalDrawings = 0;
        for (var r = summaryHeader + 1; r <= sheet.RowCount; r++)
        {
            var row = sheet.Row(r);
            var label = TrackerWorkbook.Text(row.Cell(colSummaryStatus));
            if (string.IsNullOrEmpty(label)) break;

            var packageCount = row.Cell(colPackageCount).GetInt() ?? 0;
            var drawingCount = row.Cell(colDrawingCount).GetInt() ?? 0;

            if (string.Equals(label, "Total", StringComparison.OrdinalIgnoreCase))
            {
                totalPackages = packageCount;
                totalDrawings = drawingCount;
                continue;
            }
            statuses.Add(new PackageStatusCount(
                Enum.Parse<PackageStatus>(label, true), packageCount, drawingCount));
        }

        return new ExpectedSummary(total, disciplines, packages, statuses, totalPackages, totalDrawings);
    }

    private static ExpectedRow ReadRow(
        IExcelSheet sheet, int rowNumber, string name,
        int total, int submitted, int approved, int cRevise, int dRejected, int underReview)
    {
        var row = sheet.Row(rowNumber);
        int Number(int column) => row.Cell(column).GetInt() ?? 0;
        return new ExpectedRow(name,
            Number(total), Number(submitted), Number(approved),
            Number(cRevise), Number(dRejected), Number(underReview));
    }

    private sealed record ExpectedSummary(
        ExpectedRow Total,
        IReadOnlyList<ExpectedRow> Disciplines,
        IReadOnlyList<ExpectedPackage> Packages,
        IReadOnlyList<PackageStatusCount> PackageStatuses,
        int TotalPackages,
        int TotalPackageDrawings);

    private sealed record ExpectedRow(
        string Name, int Total, int Submitted, int Approved,
        int CRevise, int DRejected, int UnderReview);

    private sealed record ExpectedPackage(
        string ActivityCode, string Package,
        int Total, int Submitted, int Approved, int UnderReview, PackageStatus Status);
}
