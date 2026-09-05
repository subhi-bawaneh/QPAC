using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Engine;

// 1. Delivered but unplanned — an Aconex revision whose document number is not in
//    the MIDP at all. Someone submitted something nobody planned.
public sealed record DeliveredButUnplanned(
    string DocumentNumber,
    string Revision,
    string Title,
    string AconexStatus,
    UnifiedStatus? Status,
    DateTime DateModified);

// 2. Unplanned in MIDP — a planned document with no Planned Start, i.e. its
//    Activity ID maps to no baseline activity (or it has none).
public sealed record UnplannedDocument(
    string Type,
    string Discipline,
    string DocumentNumber,
    string Title,
    DateTime? PlannedStart,
    string? Author);

// 3. Unused baseline packages — a Submittal activity no document points at.
public sealed record UnusedPackage(
    string Package,
    string ActivityCode,
    int OriginalDuration,
    DateTime Finish,
    int DocumentCount);

// 4. Duplicate document numbers — every row sharing a number with another row.
public sealed record DuplicateDocument(
    string Type,
    string Discipline,
    string DocumentNumber,
    string Title,
    DateTime? PlannedStart,
    string? Author,
    int Count);

public sealed record ControlFindings(
    IReadOnlyList<DeliveredButUnplanned> DeliveredButUnplanned,
    IReadOnlyList<UnplannedDocument> Unplanned,
    IReadOnlyList<UnusedPackage> UnusedPackages,
    IReadOnlyList<DuplicateDocument> Duplicates);

// The four Control Findings reports (PLAN.md § 5.4.4). Pure: no DbContext, no I/O.
// Each list keeps the order of the input, which is what the sheet's FILTER() does.
public static class ControlFindingsEngine
{
    public static ControlFindings Compute(
        IReadOnlyList<Document> documents,
        IReadOnlyList<TrackerRow> trackerRows,
        IReadOnlyList<AconexRevision> revisions,
        IReadOnlyList<BaselineActivity> baseline,
        IReadOnlyList<StatusMapping> statusMappings)
    {
        var statuses = StatusMappingLookup.Create(statusMappings);
        var rowsByDocument = trackerRows.ToDictionary(r => r.DocumentId);

        return new ControlFindings(
            DeliveredButUnplanned: FindDelivered(revisions, statuses),
            Unplanned: FindUnplanned(documents, rowsByDocument),
            UnusedPackages: FindUnusedPackages(documents, trackerRows, baseline),
            Duplicates: FindDuplicates(documents, rowsByDocument));
    }

    // Sheet: FILTER(SHD_History, (Document Length <> 3) * (In MIDP = FALSE) * (Latest = TRUE)).
    // "Document Length <> 3" is the workbook's way of saying DocNoFinal isn't the
    // "XXX" sentinel, so a row whose number could not be normalised is not a finding.
    private static IReadOnlyList<DeliveredButUnplanned> FindDelivered(
        IReadOnlyList<AconexRevision> revisions, StatusMappingLookup statuses) =>
        revisions
            .Where(r => r.IsLatest
                && !r.InMidp
                && !string.IsNullOrEmpty(r.DocNoFinal)
                && r.DocNoFinal != AconexRevision.InvalidDocNoSentinel)
            .Select(r => new DeliveredButUnplanned(
                r.DocNoFinal, r.Revision, r.Title, r.AconexStatus,
                statuses.Find(r.AconexStatus), r.DateModified))
            .ToList();

    // Sheet: FILTER(MIDP, MIDP[Planned Start] = "").
    private static IReadOnlyList<UnplannedDocument> FindUnplanned(
        IReadOnlyList<Document> documents, IReadOnlyDictionary<Guid, TrackerRow> rowsByDocument) =>
        documents
            .Where(d => PlannedStart(d, rowsByDocument) is null)
            .Select(d => new UnplannedDocument(
                d.F04DocType, d.CorporateDiscipline, d.DocumentNumber, d.Title,
                PlannedStart(d, rowsByDocument), Author(d)))
            .ToList();

    // Sheet: the Baseline Summary package rows whose Total Drawings is 0.
    private static IReadOnlyList<UnusedPackage> FindUnusedPackages(
        IReadOnlyList<Document> documents,
        IReadOnlyList<TrackerRow> trackerRows,
        IReadOnlyList<BaselineActivity> baseline)
    {
        var summary = BaselineSummaryEngine.Compute(documents, trackerRows, baseline);
        var activityByCode = baseline
            .Where(a => a.Type == BaselineActivityType.Submittal)
            .GroupBy(a => a.ActivityCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return summary.Packages
            .Where(p => p.Status == PackageStatus.Unused)
            .Select(p => new UnusedPackage(
                p.Package,
                p.ActivityCode,
                activityByCode.TryGetValue(p.ActivityCode, out var activity) ? activity.OriginalDuration : 0,
                activity?.Finish ?? default,
                p.Total))
            .ToList();
    }

    // Sheet: FILTER(MIDP, COUNTIF(MIDP[Document No], MIDP[Document No]) > 1) — every
    // row of a duplicated number, not one row per group.
    //
    // The Live schema forbids duplicates (UNIQUE on ProjectId + DocumentNumber), so
    // this reports on data imported before that constraint applied, and on Draft rows
    // which are flagged rather than rejected (PLAN.md § 5.4.4).
    private static IReadOnlyList<DuplicateDocument> FindDuplicates(
        IReadOnlyList<Document> documents, IReadOnlyDictionary<Guid, TrackerRow> rowsByDocument)
    {
        var counts = documents
            .GroupBy(d => d.DocumentNumber, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return documents
            .Where(d => counts.ContainsKey(d.DocumentNumber))
            .Select(d => new DuplicateDocument(
                d.F04DocType, d.CorporateDiscipline, d.DocumentNumber, d.Title,
                PlannedStart(d, rowsByDocument), Author(d), counts[d.DocumentNumber]))
            .ToList();
    }

    private static DateTime? PlannedStart(
        Document document, IReadOnlyDictionary<Guid, TrackerRow> rowsByDocument) =>
        rowsByDocument.TryGetValue(document.Id, out var row) ? row.PlannedStart : null;

    private static string? Author(Document document) =>
        document.Exchanges.FirstOrDefault(e => e.Number == 1)?.Author;
}
