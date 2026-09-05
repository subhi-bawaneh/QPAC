using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Engine;

// One row of the Corporate Summary — a discipline, an author, or the totals row.
// Mirrors the five panels of Tracker.xlsx!'Corporate Summary' (docs/excel-analysis.md § 4.4):
// Progress (I–M), Quality (O–U), Planned Value (W–AB), Earned Value (AD–AI).
public sealed record SummaryGroup(
    string Name,
    // Progress
    int Total,
    int Planned,
    int Submitted,
    int Approved,
    // Quality
    int QualityApproved,
    int Rejected,
    int UnderReview,
    int Withdrawn,
    int TotalRevisions,
    decimal? Quality,
    // Planned Value
    int PvPending,
    int PvSub1,
    int PvSub2,
    int PvApproved,
    decimal PlannedPercent,
    // Earned Value
    int EvPending,
    int EvSub1,
    int EvSub2,
    int EvApproved,
    decimal CompletedPercent);

public sealed record SummaryWeek(
    int Number,
    DateTime From,
    DateTime To,
    int Planned,
    int Submitted,
    int Approved,
    int CumulativePlanned,
    int CumulativeSubmitted,
    int CumulativeApproved);

public sealed record CorporateSummary(
    DateTime ReportDate,
    DateTime CurrentWeek,
    DateTime StartWeek,
    DateTime EndWeek,
    IReadOnlyList<SummaryWeek> Weeks,
    IReadOnlyList<SummaryGroup> Disciplines,
    IReadOnlyList<SummaryGroup> Authors,
    SummaryGroup Total);

// Corporate Summary (PLAN.md § 5.4.2). Pure: no DbContext, no I/O.
//
// The same row shape is produced twice — once grouped by CorporateDiscipline and once
// by the Exchange 01 author (docs/excel-analysis.md § 6, discrepancy #8).
public static class CorporateSummaryEngine
{
    public const string TotalRowName = "Total";

    public static CorporateSummary Compute(
        IReadOnlyList<Document> documents,
        IReadOnlyList<TrackerRow> trackerRows,
        Project project,
        DateTime? reportDateOverride = null)
    {
        var rowsByDocument = trackerRows.ToDictionary(r => r.DocumentId);
        var entries = documents
            .Select(d => new Entry(
                Discipline: Name(d.CorporateDiscipline),
                Author: Author(d),
                Row: rowsByDocument.TryGetValue(d.Id, out var row) ? row : Empty(d)))
            .ToList();

        var reportDate = reportDateOverride ?? project.ReportDate ?? DateTime.Today;
        var currentWeek = WeekCalendar.WeekEnd(reportDate);

        var (startWeek, endWeek) = Horizon(entries, project, currentWeek);
        var weeks = BuildWeeks(entries, startWeek, endWeek);

        return new CorporateSummary(
            ReportDate: reportDate,
            CurrentWeek: currentWeek,
            StartWeek: startWeek,
            EndWeek: endWeek,
            Weeks: weeks,
            Disciplines: GroupBy(entries, e => e.Discipline, currentWeek, project),
            // A document with no Exchange 01 author belongs to no author row: the sheet
            // counts per author name, so blanks fall out of that breakdown while still
            // counting towards their discipline and the totals row.
            Authors: GroupBy(entries.Where(e => e.Author is not null), e => e.Author!, currentWeek, project),
            Total: Summarize(TotalRowName, entries, currentWeek, project));
    }

    // StartWeek covers the earliest of any planned or actual start and the project's
    // baseline start date; EndWeek the latest planned start or actual finish.
    private static (DateTime StartWeek, DateTime EndWeek) Horizon(
        IReadOnlyList<Entry> entries, Project project, DateTime currentWeek)
    {
        var earliest = project.BaselineStartDate;
        var latest = project.BaselineStartDate;

        foreach (var entry in entries)
        {
            foreach (var date in new[] { entry.Row.PlannedStart, entry.Row.ActualStart })
            {
                if (date is not null && date < earliest) earliest = date.Value;
            }
            foreach (var date in new[] { entry.Row.PlannedStart, entry.Row.ActualFinish })
            {
                if (date is not null && date > latest) latest = date.Value;
            }
        }

        var startWeek = WeekCalendar.WeekEnd(earliest);
        var endWeek = WeekCalendar.WeekEnd(latest);
        return (startWeek, endWeek < startWeek ? startWeek : endWeek);
    }

    private static IReadOnlyList<SummaryWeek> BuildWeeks(
        IReadOnlyList<Entry> entries, DateTime startWeek, DateTime endWeek)
    {
        var planned = entries.Select(e => e.Row.PlannedStart).Where(d => d is not null)
            .Select(d => d!.Value).OrderBy(d => d).ToList();
        var submitted = entries.Select(e => e.Row.ActualStart).Where(d => d is not null)
            .Select(d => d!.Value).OrderBy(d => d).ToList();
        var approved = entries.Select(e => e.Row.ActualFinish).Where(d => d is not null)
            .Select(d => d!.Value).OrderBy(d => d).ToList();

        var weeks = new List<SummaryWeek>();
        var cumulativePlanned = 0;
        var cumulativeSubmitted = 0;
        var cumulativeApproved = 0;

        foreach (var (number, from, to) in WeekCalendar.Weeks(startWeek, endWeek))
        {
            // Half-open (From, To]: a document counts in the week its date falls in,
            // and the cumulative series is the running total for the S-curve.
            var weeklyPlanned = CountUpTo(planned, to) - CountUpTo(planned, from);
            var weeklySubmitted = CountUpTo(submitted, to) - CountUpTo(submitted, from);
            var weeklyApproved = CountUpTo(approved, to) - CountUpTo(approved, from);

            cumulativePlanned += weeklyPlanned;
            cumulativeSubmitted += weeklySubmitted;
            cumulativeApproved += weeklyApproved;

            weeks.Add(new SummaryWeek(
                number, from, to,
                weeklyPlanned, weeklySubmitted, weeklyApproved,
                cumulativePlanned, cumulativeSubmitted, cumulativeApproved));
        }
        return weeks;
    }

    // Binary search over the pre-sorted dates: the weekly loop runs ~150 times over
    // ~16k documents, so a linear count per week would be 2.4M comparisons per series.
    private static int CountUpTo(List<DateTime> sorted, DateTime limit)
    {
        var low = 0;
        var high = sorted.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (sorted[mid] <= limit) low = mid + 1;
            else high = mid;
        }
        return low;
    }

    private static IReadOnlyList<SummaryGroup> GroupBy(
        IEnumerable<Entry> entries, Func<Entry, string> key, DateTime currentWeek, Project project) =>
        entries
            .GroupBy(key, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => Summarize(g.Key, g.ToList(), currentWeek, project))
            .ToList();

    private static SummaryGroup Summarize(
        string name, IReadOnlyList<Entry> entries, DateTime currentWeek, Project project)
    {
        var total = entries.Count;
        var rows = entries.Select(e => e.Row).ToList();

        var planned = rows.Count(r => r.PlannedStart <= currentWeek);
        var submitted = rows.Count(r => r.ActualStart <= currentWeek);
        var approved = rows.Count(r => r.ActualFinish <= currentWeek);

        var qualityApproved = rows.Count(r => r.Status == UnifiedStatus.Approved);
        var rejected = rows.Count(r => r.Status == UnifiedStatus.Rejected);
        var underReview = rows.Count(r => r.Status == UnifiedStatus.UnderReview);
        var withdrawn = rows.Count(r => r.Status == UnifiedStatus.Withdrawn);

        var totalRevisions = rows.Sum(r => r.SubmissionsCount ?? 0) - withdrawn;
        var qualityDenominator = totalRevisions - underReview;
        decimal? quality = qualityDenominator == 0
            ? null
            : qualityApproved / (decimal)qualityDenominator;

        // Planned Value: Sub 2 is defined but unused in the current data — every
        // sampled row has 0 (docs/excel-analysis.md § 6, "Sub 2 always 0").
        var pvApproved = rows.Count(r => r.PlannedFinish <= currentWeek);
        var pvSub2 = 0;
        var pvSub1 = planned - pvApproved - pvSub2;
        var pvPending = total - planned;

        // Earned Value: how far each document actually got, by submission count.
        var evApproved = qualityApproved;
        var evPending = rows.Count(r => r.Status != UnifiedStatus.Approved && r.SubmissionsCount is null);
        var evSub1 = rows.Count(r => r.Status != UnifiedStatus.Approved && r.SubmissionsCount == 1);
        var evSub2 = rows.Count(r => r.Status != UnifiedStatus.Approved && r.SubmissionsCount >= 2);

        return new SummaryGroup(
            Name: name,
            Total: total,
            Planned: planned,
            Submitted: submitted,
            Approved: approved,
            QualityApproved: qualityApproved,
            Rejected: rejected,
            UnderReview: underReview,
            Withdrawn: withdrawn,
            TotalRevisions: totalRevisions,
            Quality: quality,
            PvPending: pvPending,
            PvSub1: pvSub1,
            PvSub2: pvSub2,
            PvApproved: pvApproved,
            PlannedPercent: Percent(project, pvSub1, pvSub2, pvApproved, total),
            EvPending: evPending,
            EvSub1: evSub1,
            EvSub2: evSub2,
            EvApproved: evApproved,
            CompletedPercent: Percent(project, evSub1, evSub2, evApproved, total));
    }

    private static decimal Percent(Project project, int sub1, int sub2, int approved, int total) =>
        total == 0
            ? 0m
            : (sub1 * project.WeightSub1 + sub2 * project.WeightSub2 + approved * project.WeightApproved)
              / total;

    private static string Name(string? value) => value?.Trim() ?? string.Empty;

    private static string? Author(Document document)
    {
        var author = document.Exchanges.FirstOrDefault(e => e.Number == 1)?.Author?.Trim();
        return string.IsNullOrEmpty(author) ? null : author;
    }

    // A document with no Aconex history still counts towards Total and Pending.
    private static TrackerRow Empty(Document document) => new(
        document.Id, document.DocumentNumber,
        SubmissionsCount: null, Revision: null, AconexStatus: null, Status: null,
        SubmissionDate: null, DateModified: null, Transmittal: null,
        PlannedStart: null, PlannedFinish: null, ActualStart: null, ActualFinish: null);

    private sealed record Entry(string Discipline, string? Author, TrackerRow Row);
}
