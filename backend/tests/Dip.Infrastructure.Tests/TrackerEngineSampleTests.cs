using Dip.Application.Engine;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Dip.Infrastructure.Tests;

// PLAN.md § 9 (المرحلة 5 / 5.1): compute the Tracker for 200 random documents and
// match the cached values in Tracker.xlsx — values exact, dates within one second.
// Inputs and expectations both come from that workbook (see TrackerWorkbook).
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
        using var workbook = TrackerWorkbook.Open();

        var sheetRows = TrackerWorkbook.ReadTrackerSheet(workbook);
        sheetRows.Should().HaveCountGreaterThan(15_000,
            "Tracker.xlsx!Tracker holds 15,883 rows (docs/excel-analysis.md § 4.1)");

        var revisions = TrackerWorkbook.ReadAconexHistory(workbook);
        revisions.Should().HaveCountGreaterThan(25_000,
            "Tracker.xlsx!SHD_History holds ~25,246 rows (docs/excel-analysis.md § 3.2)");

        var baseline = TrackerWorkbook.ReadBaseline(workbook);
        baseline.Should().NotBeEmpty("the Baseline sheet feeds Planned Start/Finish");

        var random = new Random(RandomSeed);
        var sample = sheetRows.OrderBy(_ => random.Next()).Take(SampleSize).ToList();
        var documents = sample.Select(TrackerWorkbook.ToDocument).ToList();

        // Tracker.xlsx!AC8 selects the Baseline schedule, so Planned Start/Finish
        // come from the Baseline sheet rather than the Delivery Milestone.
        var project = new Domain.Entities.Project { ScheduleMode = ScheduleMode.Baseline };

        var computed = TrackerEngine
            .Compute(documents, revisions, baseline, TrackerWorkbook.SeededStatusMappings(), project)
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

    // The 200-row sample is the PLAN.md § 9 target; this is the regression guard for a
    // change to how the engine picks a document's current revision (RevisionOrder,
    // finding 25). It runs the whole population — every document the Tracker sheet
    // holds — so "no disagreements" means no disagreements anywhere, not in a sample.
    //
    // Scope: the columns derived from the Aconex history, which are the only ones the
    // revision comparer can move. Planned Start and Planned Finish come from the
    // Baseline sheet before any revision is looked at; the sample holds 54 such
    // differences on 27 documents, which pre-date this change and are reported
    // separately rather than silently asserted away.
    [Fact]
    public void EveryDocument_MatchesTheTrackerSheet_OnTheAconexDerivedColumns()
    {
        using var workbook = TrackerWorkbook.Open();

        var sheetRows = TrackerWorkbook.ReadTrackerSheet(workbook);
        var revisions = TrackerWorkbook.ReadAconexHistory(workbook);
        var baseline = TrackerWorkbook.ReadBaseline(workbook);
        var project = new Domain.Entities.Project { ScheduleMode = ScheduleMode.Baseline };

        // The sheet repeats some numbers (see below), so the computed rows are keyed by
        // number rather than assumed unique; every sheet row is still compared, which
        // is what makes a repeat that disagrees with itself visible.
        var computed = TrackerEngine
            .Compute(
                sheetRows.Select(TrackerWorkbook.ToDocument).ToList(),
                revisions, baseline, TrackerWorkbook.SeededStatusMappings(), project)
            .GroupBy(r => r.DocumentNumber, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var repeated = sheetRows
            .GroupBy(r => r.DocumentNumber, StringComparer.Ordinal)
            .Count(g => g.Count() > 1);

        var failures = new List<string>();
        foreach (var expected in sheetRows)
        {
            Compare(expected, computed[expected.DocumentNumber], failures);
        }

        var schedule = failures
            .Where(f => f.Contains(" Planned Start:", StringComparison.Ordinal)
                     || f.Contains(" Planned Finish:", StringComparison.Ordinal))
            .ToList();
        var aconexDerived = failures.Except(schedule).ToList();

        _output.WriteLine(
            $"{sheetRows.Count} rows compared over {computed.Count} distinct numbers "
            + $"({repeated} numbers appear more than once); "
            + $"{aconexDerived.Count} disagreements on the Aconex-derived columns, "
            + $"{schedule.Count} on Planned Start/Finish (pre-existing, out of scope)");
        foreach (var failure in aconexDerived.Take(25)) _output.WriteLine(failure);

        aconexDerived.Should().BeEmpty(
            "the revision comparer must reproduce Tracker.xlsx for every row "
            + "({0} differences across {1} documents)",
            aconexDerived.Count, sheetRows.Count);
    }

    private static void Compare(
        TrackerWorkbook.TrackerSheetRow expected, TrackerRow actual, List<string> failures)
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
}
