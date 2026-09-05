using Dip.Application.Abstractions;
using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Dip.Infrastructure.Tests;

// PLAN.md § 9 (المرحلة 5 / 5.4): the four Control Findings reports must match
// Tracker.xlsx!'Control Findings' — both the headline counts in row 3 and the rows
// themselves.
//
// The sheet's four blocks and their FILTER criteria:
//   B-I   Aconex vs MIDP     SHD_History where Document Length <> 3, In MIDP = FALSE, Latest = TRUE
//   K-R   Unplanned in MIDP  MIDP where Planned Start is blank
//   T-X   Unused Packages    Baseline Summary package rows with 0 drawings
//   Z-AF  Duplicate Doc No   MIDP where COUNTIF(Document No, Document No) > 1
public class ControlFindingsSampleTests
{
    private readonly ITestOutputHelper _output;

    public ControlFindingsSampleTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ReproducesTheControlFindingsSheet()
    {
        using var workbook = TrackerWorkbook.Open();

        var sheetRows = TrackerWorkbook.ReadTrackerSheet(workbook);
        var documents = sheetRows.Select(TrackerWorkbook.ToDocument).ToList();
        var revisions = TrackerWorkbook.ReadAconexHistory(workbook);
        var baseline = TrackerWorkbook.ReadBaseline(workbook);
        var statusMappings = TrackerWorkbook.SeededStatusMappings();

        var project = new Project { ScheduleMode = ScheduleMode.Baseline };
        var trackerRows = TrackerEngine.Compute(documents, revisions, baseline, statusMappings, project);

        var findings = ControlFindingsEngine.Compute(
            documents, trackerRows, revisions, baseline, statusMappings);

        var expected = ReadFindingsSheet(workbook);

        // Anchors: the counts the sheet prints above each block (row 3).
        expected.DeliveredCount.Should().Be(29);
        expected.UnplannedCount.Should().Be(136);
        expected.UnusedPackageCount.Should().Be(190);
        expected.DuplicateCount.Should().Be(57);

        var failures = new List<string>();

        CompareSets(failures, "Aconex vs MIDP",
            expected.Delivered, findings.DeliveredButUnplanned.Select(d => d.DocumentNumber));
        CompareSets(failures, "Unplanned in MIDP",
            expected.Unplanned, findings.Unplanned.Select(d => d.DocumentNumber));
        CompareSets(failures, "Unused Baseline Packages",
            expected.UnusedPackages, findings.UnusedPackages.Select(p => p.ActivityCode));
        CompareSets(failures, "Duplicate Document No",
            expected.Duplicates, findings.Duplicates.Select(d => d.DocumentNumber));

        foreach (var failure in failures.Take(30)) _output.WriteLine(failure);
        failures.Should().BeEmpty(
            "the engine must reproduce the Control Findings sheet ({0} differences)", failures.Count);
    }

    [Fact]
    public void CarriesTheDetailColumnsEachReportIsRenderedWith()
    {
        using var workbook = TrackerWorkbook.Open();

        var sheetRows = TrackerWorkbook.ReadTrackerSheet(workbook);
        var documents = sheetRows.Select(TrackerWorkbook.ToDocument).ToList();
        var revisions = TrackerWorkbook.ReadAconexHistory(workbook);
        var baseline = TrackerWorkbook.ReadBaseline(workbook);
        var statusMappings = TrackerWorkbook.SeededStatusMappings();

        var project = new Project { ScheduleMode = ScheduleMode.Baseline };
        var trackerRows = TrackerEngine.Compute(documents, revisions, baseline, statusMappings, project);
        var findings = ControlFindingsEngine.Compute(
            documents, trackerRows, revisions, baseline, statusMappings);

        // Report 1 carries the raw Aconex status and its unified mapping.
        var delivered = findings.DeliveredButUnplanned
            .Single(d => d.DocumentNumber == "QF01012-NES-C04518-SDW-ELP-05-CENT01-2B10006");
        delivered.Revision.Should().Be("01");
        delivered.AconexStatus.Should().Be("B - Approved with Comments");
        delivered.Status.Should().Be(UnifiedStatus.Approved);
        delivered.Title.Should().StartWith("EARTHING & LIGHTNING");
        delivered.DateModified.Date.Should().Be(new DateTime(2026, 3, 5));

        // Report 2: every row really has no planned start, and carries its author.
        findings.Unplanned.Should().OnlyContain(u => u.PlannedStart == null);
        findings.Unplanned.Should().Contain(u => u.Author == "Nesma & Partners");
        findings.Unplanned.Should().OnlyContain(u => !string.IsNullOrEmpty(u.DocumentNumber));

        // Report 3 matches the Baseline Summary's Unused packages exactly.
        var baselineSummary = BaselineSummaryEngine.Compute(documents, trackerRows, baseline);
        findings.UnusedPackages.Select(p => p.ActivityCode).Should().BeEquivalentTo(
            baselineSummary.Packages.Where(p => p.Status == PackageStatus.Unused)
                .Select(p => p.ActivityCode));
        findings.UnusedPackages.Should().OnlyContain(p => p.DocumentCount == 0);

        // Report 4 lists every row of a duplicated number, each carrying its group size.
        findings.Duplicates.Should().OnlyContain(d => d.Count > 1);
        foreach (var group in findings.Duplicates.GroupBy(d => d.DocumentNumber))
        {
            group.Should().HaveCount(group.First().Count,
                "the report lists every row of a duplicated number");
        }
    }

    private static void CompareSets(
        List<string> failures, string report,
        IReadOnlyList<string> expected, IEnumerable<string> actual)
    {
        var actualList = actual.ToList();
        if (expected.Count != actualList.Count)
        {
            failures.Add($"{report}: expected {expected.Count} rows, got {actualList.Count}");
        }

        foreach (var missing in expected.Except(actualList, StringComparer.OrdinalIgnoreCase).Take(5))
        {
            failures.Add($"{report}: '{missing}' is in the sheet but not in the computed report");
        }
        foreach (var extra in actualList.Except(expected, StringComparer.OrdinalIgnoreCase).Take(5))
        {
            failures.Add($"{report}: '{extra}' was computed but is not in the sheet");
        }
    }

    // ------------------------------------------------------------------ sheet

    private static ExpectedFindings ReadFindingsSheet(IExcelWorkbook workbook)
    {
        var sheet = workbook.Sheet("Control Findings");
        var headerRow = TrackerWorkbook.HeaderRow(sheet, "Date Modified");
        var countRow = TrackerWorkbook.HeaderRow(sheet, "Documents Submitted but not in MIDP") + 1;

        // Each block repeats a "Sr." column; the value columns follow it.
        var serialColumns = Enumerable.Range(1, sheet.ColumnCount)
            .Where(c => string.Equals(sheet.Row(headerRow).Cell(c).GetStringOrNull()?.Trim(), "Sr.",
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        serialColumns.Should().HaveCount(4, "the sheet has four report blocks");

        var delivered = ReadColumn(sheet, headerRow, "Document No", serialColumns[0]);
        var unplanned = ReadColumn(sheet, headerRow, "Document No", serialColumns[1]);
        var unusedPackages = ReadColumn(sheet, headerRow, "Activity Code", serialColumns[2]);
        var duplicates = ReadColumn(sheet, headerRow, "Document No", serialColumns[3]);

        return new ExpectedFindings(
            DeliveredCount: CountCell(sheet, countRow, serialColumns[0]),
            UnplannedCount: CountCell(sheet, countRow, serialColumns[1]),
            UnusedPackageCount: CountCell(sheet, countRow, serialColumns[2]),
            DuplicateCount: CountCell(sheet, countRow, serialColumns[3]),
            Delivered: delivered,
            Unplanned: unplanned,
            UnusedPackages: unusedPackages,
            Duplicates: duplicates);
    }

    // The block counts sit under the block title, one column right of its "Sr." column.
    private static int CountCell(IExcelSheet sheet, int rowNumber, int serialColumn) =>
        sheet.Row(rowNumber).Cell(serialColumn + 1).GetInt()
        ?? throw new InvalidOperationException($"No count above column {serialColumn + 1}");

    // Reads the named column of one block: the first matching header at or right of
    // that block's "Sr." column, down to the first blank.
    private static IReadOnlyList<string> ReadColumn(
        IExcelSheet sheet, int headerRow, string header, int fromColumn)
    {
        var row = sheet.Row(headerRow);
        var column = -1;
        for (var c = fromColumn; c <= sheet.ColumnCount; c++)
        {
            if (string.Equals(row.Cell(c).GetStringOrNull()?.Trim(), header, StringComparison.OrdinalIgnoreCase))
            {
                column = c;
                break;
            }
        }
        if (column < 0) throw new InvalidOperationException($"No '{header}' column after {fromColumn}");

        var values = new List<string>();
        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var value = sheet.Row(r).Cell(column).GetStringOrNull()?.Trim();
            if (string.IsNullOrEmpty(value)) break;
            values.Add(value);
        }
        return values;
    }

    private sealed record ExpectedFindings(
        int DeliveredCount,
        int UnplannedCount,
        int UnusedPackageCount,
        int DuplicateCount,
        IReadOnlyList<string> Delivered,
        IReadOnlyList<string> Unplanned,
        IReadOnlyList<string> UnusedPackages,
        IReadOnlyList<string> Duplicates);
}
