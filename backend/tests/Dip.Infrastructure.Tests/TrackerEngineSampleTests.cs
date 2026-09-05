using Dip.Application.Abstractions;
using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Excel;
using Dip.Infrastructure.Seeding;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Dip.Infrastructure.Tests;

// PLAN.md § 9 (المرحلة 5 / 5.1): compute the Tracker for 200 random documents and
// match the cached values in Tracker.xlsx — values exact, dates within one second.
//
// Every input is taken from the same workbook that produced the expected output, and
// from the columns the workbook's own formulas read:
//   Tracker      -> the documents (No, Activity ID, Delivery Milestone) and the
//                   expected computed columns
//   SHD_History  -> the Aconex revisions, keyed on `Document No Final` and
//                   `Terminated`, which is exactly what every Tracker MAXIFS/MINIFS
//                   matches on
//   Baseline     -> the planned dates
//
// `Document No Final` is a static (formula-free) column in the workbook, so it is an
// input here rather than something to recompute — see docs/excel-analysis.md § 6.
// The importer's own normalisation is covered by NormalizeDocNoTests.
//
// No database: the engine is pure, so this test only needs the workbook.
public class TrackerEngineSampleTests
{
    private const int SampleSize = 200;
    private const int RandomSeed = 20260905;      // fixed so a failure is reproducible
    private static readonly TimeSpan DateTolerance = TimeSpan.FromSeconds(1);

    private readonly ITestOutputHelper _output;

    public TrackerEngineSampleTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Computes200RandomDocuments_MatchingTheTrackerSheet()
    {
        var reader = new ClosedXmlReader();
        var path = SampleFiles.Path("Tracker.xlsx");
        using var workbook = reader.Open(path);

        var expectations = ReadTrackerSheet(workbook);
        expectations.Should().HaveCountGreaterThan(15_000,
            "Tracker.xlsx!Tracker holds 15,883 rows (docs/excel-analysis.md § 4.1)");

        var revisions = ReadAconexHistory(workbook);
        revisions.Should().HaveCountGreaterThan(25_000,
            "Tracker.xlsx!SHD_History holds ~25,246 rows (docs/excel-analysis.md § 3.2)");

        var baseline = ReadBaseline(workbook);
        baseline.Should().NotBeEmpty("the Baseline sheet feeds Planned Start/Finish");

        var sample = Sample(expectations, SampleSize);
        var documents = sample.Select(e => new Document
        {
            Id = Guid.NewGuid(),
            DocumentNumber = e.DocumentNumber,
            ActivityId = e.ActivityId,
            DeliveryMilestone = e.DeliveryMilestone,
            CorporateDiscipline = e.Discipline ?? string.Empty,
        }).ToList();

        // Tracker.xlsx!AC8 selects the Baseline schedule, so Planned Start/Finish
        // come from the Baseline sheet rather than the Delivery Milestone.
        var project = new Project { ScheduleMode = ScheduleMode.Baseline };

        var computed = TrackerEngine
            .Compute(documents, revisions, baseline, SeededStatusMappings(), project)
            .ToDictionary(r => r.DocumentNumber, StringComparer.Ordinal);

        // Guard against a silently empty comparison: a regression that returned nulls
        // everywhere would otherwise "match" documents that have no Aconex history.
        sample.Count(e => e.Revision is not null).Should().BeGreaterThan(50,
            "the sample must exercise documents that carry Aconex history");
        sample.Count(e => e.PlannedStart is not null).Should().BeGreaterThan(50,
            "the sample must exercise documents planned from the Baseline sheet");

        var failures = new List<string>();
        foreach (var expected in sample)
        {
            Compare(expected, computed[expected.DocumentNumber], failures);
        }

        foreach (var failure in failures.Take(25)) _output.WriteLine(failure);

        failures.Should().BeEmpty(
            "the engine must reproduce Tracker.xlsx ({0} differences across {1} sampled rows)",
            failures.Count, sample.Count);
    }

    private static void Compare(TrackerExpectation expected, TrackerRow actual, List<string> failures)
    {
        void Check(string column, object? want, object? got)
        {
            if (!Equals(want, got))
            {
                failures.Add($"{expected.DocumentNumber} {column}: expected {Show(want)}, got {Show(got)}");
            }
        }

        void CheckDate(string column, DateTime? want, DateTime? got)
        {
            var ok = want is null
                ? got is null
                : got is not null && (got.Value - want.Value).Duration() <= DateTolerance;
            if (!ok)
            {
                failures.Add($"{expected.DocumentNumber} {column}: expected {Show(want)}, got {Show(got)}");
            }
        }

        Check("# of Submissions", expected.SubmissionsCount, actual.SubmissionsCount);
        Check("Revision", expected.Revision, actual.Revision);
        Check("Aconex Status", expected.AconexStatus, actual.AconexStatus);
        Check("Status", expected.Status, Display(actual.Status));
        Check("Transmittal", expected.Transmittal, actual.Transmittal);
        CheckDate("Submission Date", expected.SubmissionDate, actual.SubmissionDate);
        CheckDate("Date Modified", expected.DateModified, actual.DateModified);
        CheckDate("Planned Start", expected.PlannedStart, actual.PlannedStart);
        CheckDate("Planned Finish", expected.PlannedFinish, actual.PlannedFinish);
        CheckDate("Actual Start", expected.ActualStart, actual.ActualStart);
        CheckDate("Actual Finish", expected.ActualFinish, actual.ActualFinish);
    }

    // The sheet writes the Lists-sheet labels, which differ from the enum names.
    private static string? Display(UnifiedStatus? status) => status switch
    {
        UnifiedStatus.Approved => "Approved",
        UnifiedStatus.Rejected => "Rejected",
        UnifiedStatus.UnderReview => "Under Review",
        UnifiedStatus.Withdrawn => "Withdrawn",
        _ => null,
    };

    private static string Show(object? value) => value switch
    {
        null => "<null>",
        DateTime d => d.ToString("O"),
        _ => $"'{value}'",
    };

    private static IReadOnlyList<StatusMapping> SeededStatusMappings() => SeedData.StatusMappings
        .Select(m => new StatusMapping
        {
            AconexStatus = m.AconexStatus,
            Status = m.Status,
            IsLegacy = m.IsLegacy,
        })
        .ToList();

    private static IReadOnlyList<TrackerExpectation> Sample(
        IReadOnlyList<TrackerExpectation> rows, int count)
    {
        var random = new Random(RandomSeed);
        return rows.OrderBy(_ => random.Next()).Take(count).ToList();
    }

    // ------------------------------------------------------------------ workbook

    private static IReadOnlyList<TrackerExpectation> ReadTrackerSheet(IExcelWorkbook workbook)
    {
        var sheet = workbook.Sheet("Tracker");
        var headerRow = HeaderRow(sheet, "Document No");

        var colDocumentNo = Column(sheet, headerRow, "Document No");
        var colDiscipline = Column(sheet, headerRow, "Discipline");
        var colMilestone = Column(sheet, headerRow, "Delivery Milestone");
        var colActivityId = Column(sheet, headerRow, "Activity ID");
        var colSubmissions = Column(sheet, headerRow, "# of Submissions");
        var colRevision = Column(sheet, headerRow, "Revision");
        var colAconexStatus = Column(sheet, headerRow, "Aconex Status");
        var colStatus = Column(sheet, headerRow, "Status");
        var colSubmissionDate = Column(sheet, headerRow, "Submission Date");
        var colDateModified = Column(sheet, headerRow, "Date Modified");
        var colTransmittal = Column(sheet, headerRow, "Transmittal");
        var colPlannedStart = Column(sheet, headerRow, "Planned Start");
        var colPlannedFinish = Column(sheet, headerRow, "Planned Finish");
        var colActualStart = Column(sheet, headerRow, "Actual Start");
        var colActualFinish = Column(sheet, headerRow, "Actual Finish");

        var rows = new List<TrackerExpectation>();
        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var row = sheet.Row(r);
            var documentNumber = row.Cell(colDocumentNo).GetStringOrNull()?.Trim();
            if (string.IsNullOrEmpty(documentNumber)) continue;

            rows.Add(new TrackerExpectation(
                DocumentNumber: documentNumber,
                Discipline: Text(row.Cell(colDiscipline)),
                DeliveryMilestone: row.Cell(colMilestone).GetDateTime(),
                ActivityId: Text(row.Cell(colActivityId)),
                SubmissionsCount: row.Cell(colSubmissions).GetInt(),
                Revision: Text(row.Cell(colRevision)),
                AconexStatus: Text(row.Cell(colAconexStatus)),
                Status: Text(row.Cell(colStatus)),
                SubmissionDate: row.Cell(colSubmissionDate).GetDateTime(),
                DateModified: row.Cell(colDateModified).GetDateTime(),
                Transmittal: Text(row.Cell(colTransmittal)),
                PlannedStart: row.Cell(colPlannedStart).GetDateTime(),
                PlannedFinish: row.Cell(colPlannedFinish).GetDateTime(),
                ActualStart: row.Cell(colActualStart).GetDateTime(),
                ActualFinish: row.Cell(colActualFinish).GetDateTime()));
        }
        return rows;
    }

    private static IReadOnlyList<AconexRevision> ReadAconexHistory(IExcelWorkbook workbook)
    {
        var sheet = workbook.Sheet("SHD_History");
        var headerRow = HeaderRow(sheet, "Document No Final");

        var colDocNo = Column(sheet, headerRow, "Document No");
        var colDocNoFinal = Column(sheet, headerRow, "Document No Final");
        var colRevision = Column(sheet, headerRow, "Revision");
        var colStatus = Column(sheet, headerRow, "Status");
        var colReviewStatus = Column(sheet, headerRow, "Review Status");
        var colDateModified = Column(sheet, headerRow, "Date Modified");
        var colTransmittal = Column(sheet, headerRow, "Transmittal In");
        var colTerminated = Column(sheet, headerRow, "Terminated");

        var rows = new List<AconexRevision>();
        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var row = sheet.Row(r);
            var dateModified = row.Cell(colDateModified).GetDateTime();
            if (dateModified is null) continue;

            rows.Add(new AconexRevision
            {
                AconexDocNo = Text(row.Cell(colDocNo)) ?? string.Empty,
                DocNoFinal = Text(row.Cell(colDocNoFinal)) ?? string.Empty,
                Revision = Text(row.Cell(colRevision)) ?? string.Empty,
                AconexStatus = Text(row.Cell(colStatus)) ?? string.Empty,
                ReviewStatus = Text(row.Cell(colReviewStatus)),
                DateModified = dateModified.Value,
                TransmittalIn = Text(row.Cell(colTransmittal)),
                IsTerminated = string.Equals(
                    Text(row.Cell(colTerminated)), "TRUE", StringComparison.OrdinalIgnoreCase),
            });
        }
        return rows;
    }

    private static IReadOnlyList<BaselineActivity> ReadBaseline(IExcelWorkbook workbook)
    {
        var sheet = workbook.Sheet("Baseline");
        var headerRow = HeaderRow(sheet, "Activity Code");

        var colActivityCode = Column(sheet, headerRow, "Activity Code");
        var colActivity = Column(sheet, headerRow, "Activity");
        var colPackage = Column(sheet, headerRow, "Package");
        var colStart = Column(sheet, headerRow, "Start");
        var colFinish = Column(sheet, headerRow, "Finish");

        var rows = new List<BaselineActivity>();
        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var row = sheet.Row(r);
            var code = Text(row.Cell(colActivityCode));
            if (string.IsNullOrEmpty(code)) continue;

            rows.Add(new BaselineActivity
            {
                ActivityCode = code,
                Package = Text(row.Cell(colPackage)) ?? string.Empty,
                Type = string.Equals(Text(row.Cell(colActivity)), "Approval", StringComparison.OrdinalIgnoreCase)
                    ? BaselineActivityType.Approval
                    : BaselineActivityType.Submittal,
                Start = row.Cell(colStart).GetDateTime() ?? default,
                Finish = row.Cell(colFinish).GetDateTime() ?? default,
            });
        }
        return rows;
    }

    private static int HeaderRow(IExcelSheet sheet, string anyHeader) =>
        sheet.FindRowContainingAnywhere(anyHeader)
        ?? throw new InvalidOperationException($"Sheet '{sheet.Name}' has no '{anyHeader}' header");

    private static int Column(IExcelSheet sheet, int headerRow, string header)
    {
        var row = sheet.Row(headerRow);
        for (var c = 1; c <= sheet.ColumnCount; c++)
        {
            if (string.Equals(row.Cell(c).GetStringOrNull()?.Trim(), header, StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }
        throw new InvalidOperationException($"Sheet '{sheet.Name}' has no '{header}' column");
    }

    // Blank cells and the "0" the export writes for a missing transmittal are null.
    private static string? Text(IExcelCell cell)
    {
        var value = cell.GetStringOrNull()?.Trim();
        return string.IsNullOrEmpty(value) || value == "0" ? null : value;
    }

    private sealed record TrackerExpectation(
        string DocumentNumber,
        string? Discipline,
        DateTime? DeliveryMilestone,
        string? ActivityId,
        int? SubmissionsCount,
        string? Revision,
        string? AconexStatus,
        string? Status,
        DateTime? SubmissionDate,
        DateTime? DateModified,
        string? Transmittal,
        DateTime? PlannedStart,
        DateTime? PlannedFinish,
        DateTime? ActualStart,
        DateTime? ActualFinish);
}
