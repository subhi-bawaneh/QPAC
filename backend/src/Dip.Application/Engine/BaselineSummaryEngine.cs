using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Engine;

// One discipline row of the Baseline Summary, or the project totals row.
// Submitted/Approved are date-driven, matching the sheet's
// COUNTIFS(..., [Actual Start], ">"&0) — a document counts as submitted once it
// has an actual start, regardless of its current status.
public sealed record BaselineDisciplineRow(
    string Name,
    int Total,
    int Submitted,
    int Approved,
    int CRevise,
    int DRejected,
    int UnderReview);

// One baseline package: a Submittal activity plus the documents pointing at it
// through Document.ActivityId.
public sealed record BaselinePackageRow(
    string ActivityCode,
    string Package,
    int Total,
    int Submitted,
    int Approved,
    int CRevise,
    int DRejected,
    int UnderReview,
    PackageStatus Status);

public sealed record PackageStatusCount(PackageStatus Status, int Packages, int Drawings);

public sealed record BaselineSummary(
    BaselineDisciplineRow Total,
    IReadOnlyList<BaselineDisciplineRow> Disciplines,
    IReadOnlyList<BaselinePackageRow> Packages,
    IReadOnlyList<PackageStatusCount> PackageStatuses,
    int TotalPackages,
    int TotalPackageDrawings);

// Baseline Summary (PLAN.md § 5.4.3). Pure: no DbContext, no I/O.
public static class BaselineSummaryEngine
{
    public const string TotalRowName = "Total";
    public const string CReviseStatus = "C - Revise and Resubmit";
    public const string DRejectedStatus = "D - Rejected";

    private static readonly string CReviseKey = StatusMappingLookup.Normalize(CReviseStatus);
    private static readonly string DRejectedKey = StatusMappingLookup.Normalize(DRejectedStatus);

    public static BaselineSummary Compute(
        IReadOnlyList<Document> documents,
        IReadOnlyList<TrackerRow> trackerRows,
        IReadOnlyList<BaselineActivity> baseline)
    {
        var rowsByDocument = trackerRows.ToDictionary(r => r.DocumentId);
        var entries = documents
            .Select(d => new Entry(
                Discipline: d.CorporateDiscipline?.Trim() ?? string.Empty,
                ActivityId: d.ActivityId?.Trim(),
                Row: rowsByDocument.TryGetValue(d.Id, out var row) ? row : null))
            .ToList();

        var disciplines = entries
            .GroupBy(e => e.Discipline, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => Summarize(g.Key, g.ToList()))
            .ToList();

        var byActivity = entries
            .Where(e => !string.IsNullOrEmpty(e.ActivityId))
            .GroupBy(e => e.ActivityId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Entry>)g.ToList(), StringComparer.OrdinalIgnoreCase);

        // One row per Submittal activity: the Approval half of a package is the
        // delivery date pair, not a package of its own (PLAN.md § 5.1.4).
        var packages = baseline
            .Where(a => a.Type == BaselineActivityType.Submittal && !string.IsNullOrEmpty(a.ActivityCode))
            .GroupBy(a => a.ActivityCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => Package(
                g.Key,
                g.First().Package,
                byActivity.TryGetValue(g.Key, out var found) ? found : Array.Empty<Entry>()))
            .ToList();

        var statuses = Enum.GetValues<PackageStatus>()
            .Select(status => new PackageStatusCount(
                status,
                packages.Count(p => p.Status == status),
                packages.Where(p => p.Status == status).Sum(p => p.Total)))
            .ToList();

        return new BaselineSummary(
            Total: Summarize(TotalRowName, entries),
            Disciplines: disciplines,
            Packages: packages,
            PackageStatuses: statuses,
            TotalPackages: packages.Count,
            TotalPackageDrawings: packages.Sum(p => p.Total));
    }

    private static BaselinePackageRow Package(string activityCode, string package, IReadOnlyList<Entry> entries)
    {
        var counts = Summarize(activityCode, entries);
        return new BaselinePackageRow(
            ActivityCode: activityCode,
            Package: package,
            Total: counts.Total,
            Submitted: counts.Submitted,
            Approved: counts.Approved,
            CRevise: counts.CRevise,
            DRejected: counts.DRejected,
            UnderReview: counts.UnderReview,
            Status: StatusOf(counts.Total, counts.Submitted));
    }

    public static PackageStatus StatusOf(int total, int submitted) => total switch
    {
        0 => PackageStatus.Unused,
        _ when submitted == total => PackageStatus.Submitted,
        _ when submitted == 0 => PackageStatus.Pending,
        _ => PackageStatus.Partial,
    };

    private static BaselineDisciplineRow Summarize(string name, IReadOnlyList<Entry> entries)
    {
        var rows = entries.Select(e => e.Row).ToList();

        return new BaselineDisciplineRow(
            Name: name,
            Total: entries.Count,
            Submitted: rows.Count(r => r?.ActualStart is not null),
            Approved: rows.Count(r => r?.ActualFinish is not null),
            // The sheet's own C-Revise / D-Rejected columns compare the Aconex status
            // against a header that drops the spaces around the dash, so they read 0
            // everywhere. These count the real statuses, per PLAN.md § 5.4.3 —
            // see docs/excel-analysis.md § 6, finding 20.
            CRevise: rows.Count(r => Is(r?.AconexStatus, CReviseKey)),
            DRejected: rows.Count(r => Is(r?.AconexStatus, DRejectedKey)),
            UnderReview: rows.Count(r => r?.Status == UnifiedStatus.UnderReview));
    }

    private static bool Is(string? aconexStatus, string normalizedKey) =>
        aconexStatus is not null
        && string.Equals(StatusMappingLookup.Normalize(aconexStatus), normalizedKey, StringComparison.Ordinal);

    private sealed record Entry(string Discipline, string? ActivityId, TrackerRow? Row);
}
