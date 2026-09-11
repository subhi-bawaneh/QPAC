using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Engine;

// What a finding points at, so the grid can open it. Two of the five have no document
// to open — a delivered-but-unplanned row is an Aconex revision by definition, and an
// unused package is a baseline activity — so the kind travels with the id rather than
// every finding pretending to carry a DocumentId it does not have.
public enum FindingSource
{
    Document = 0,
    AconexRevision = 1,
    BaselineActivity = 2,
}

// 1. Delivered but unplanned — an Aconex revision whose document number is not in
//    the MIDP at all. Someone submitted something nobody planned.
public sealed record DeliveredButUnplanned(
    Guid SourceId,
    FindingSource SourceKind,
    string DocumentNumber,
    string Revision,
    string Title,
    string AconexStatus,
    UnifiedStatus? Status,
    DateTime DateModified);

// 2. Unplanned in MIDP — a planned document with no Planned Start, i.e. its
//    Activity ID maps to no baseline activity (or it has none).
public sealed record UnplannedDocument(
    Guid SourceId,
    FindingSource SourceKind,
    Guid DocumentId,
    string Type,
    string Discipline,
    string DocumentNumber,
    string Title,
    DateTime? PlannedStart,
    string? Author);

// 3. Unused baseline packages — a Submittal activity no document points at.
public sealed record UnusedPackage(
    Guid SourceId,
    FindingSource SourceKind,
    string Package,
    string ActivityCode,
    int OriginalDuration,
    DateTime Finish,
    int DocumentCount);

// 4. Duplicate document numbers — every row sharing a number with another row.
public sealed record DuplicateDocument(
    Guid SourceId,
    FindingSource SourceKind,
    Guid DocumentId,
    string Type,
    string Discipline,
    string DocumentNumber,
    string Title,
    DateTime? PlannedStart,
    string? Author,
    int Count);

// 5. A numbering field holding a value that is not in its picklist. Named field and
//    named value: "Segment 7 holds MBLAD2, which is not in the Building list" is fixed
//    in a minute, where "invalid number" sits open for months.
//
//    Off-list rows import and are flagged rather than being rejected. The 503 such rows
//    in the sample split three ways — stale picklists, data-entry errors, and one Excel
//    autofill accident — and only a person can tell them apart, so they have to be
//    visible rather than blocked.
public sealed record OffListSegment(
    Guid SourceId,
    FindingSource SourceKind,
    Guid DocumentId,
    string DocumentNumber,
    string Field,
    string Value,
    string Discipline,
    string Title);

public sealed record ControlFindings(
    IReadOnlyList<DeliveredButUnplanned> DeliveredButUnplanned,
    IReadOnlyList<UnplannedDocument> Unplanned,
    IReadOnlyList<UnusedPackage> UnusedPackages,
    IReadOnlyList<DuplicateDocument> Duplicates,
    IReadOnlyList<OffListSegment> OffList);

// The five Control Findings reports (PLAN.md § 5.4.4, plus the off-list segments).
// Pure: no DbContext, no I/O.
// Each list keeps the order of the input, which is what the sheet's FILTER() does.
public static class ControlFindingsEngine
{
    public static ControlFindings Compute(
        IReadOnlyList<Document> documents,
        IReadOnlyList<TrackerRow> trackerRows,
        IReadOnlyList<AconexRevision> revisions,
        IReadOnlyList<BaselineActivity> baseline,
        IReadOnlyList<StatusMapping> statusMappings,
        IReadOnlyList<PicklistItem>? picklists = null)
    {
        var statuses = StatusMappingLookup.Create(statusMappings);
        var rowsByDocument = trackerRows.ToDictionary(r => r.DocumentId);

        // Membership is decided here, against the documents this run was given, rather
        // than read from a flag stamped at import time: under append semantics the
        // events usually arrive before the documents do, so a stored flag is stale by
        // construction.
        var planned = documents
            .Select(d => d.DocumentNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new ControlFindings(
            DeliveredButUnplanned: FindDelivered(revisions, planned, statuses),
            Unplanned: FindUnplanned(documents, rowsByDocument),
            UnusedPackages: FindUnusedPackages(documents, trackerRows, baseline),
            Duplicates: FindDuplicates(documents, rowsByDocument),
            OffList: FindOffList(documents, picklists ?? Array.Empty<PicklistItem>()));
    }

    // Sheet: FILTER(SHD_History, (Document Length <> 3) * (In MIDP = FALSE) * (Latest = TRUE)).
    // "Document Length <> 3" is the workbook's way of saying the row has a usable
    // number, so a row whose raw value could not be normalised is not a finding.
    // A terminated revision is not one either: it was withdrawn, not delivered.
    private static IReadOnlyList<DeliveredButUnplanned> FindDelivered(
        IReadOnlyList<AconexRevision> revisions,
        IReadOnlySet<string> planned,
        StatusMappingLookup statuses) =>
        revisions
            .Where(r => r.IsLatest
                && !r.IsTerminated
                && !string.IsNullOrEmpty(r.DocNoFinal)
                && !planned.Contains(r.DocNoFinal!))
            .Select(r => new DeliveredButUnplanned(
                r.Id, FindingSource.AconexRevision,
                r.DocNoFinal!, r.Revision, r.Title, r.AconexStatus,
                statuses.Find(r.AconexStatus), r.DateModified))
            .ToList();

    // Sheet: FILTER(MIDP, MIDP[Planned Start] = "").
    private static IReadOnlyList<UnplannedDocument> FindUnplanned(
        IReadOnlyList<Document> documents, IReadOnlyDictionary<Guid, TrackerRow> rowsByDocument) =>
        documents
            .Where(d => PlannedStart(d, rowsByDocument) is null)
            .Select(d => new UnplannedDocument(
                d.Id, FindingSource.Document, d.Id,
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
                activityByCode.TryGetValue(p.ActivityCode, out var found) ? found.Id : Guid.Empty,
                FindingSource.BaselineActivity,
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
                d.Id, FindingSource.Document, d.Id,
                d.F04DocType, d.CorporateDiscipline, d.DocumentNumber, d.Title,
                PlannedStart(d, rowsByDocument), Author(d), counts[d.DocumentNumber]))
            .ToList();
    }

    // Lookup, never a length or shape check: SECE001 is seven characters and valid,
    // and Z00000 is six and valid. A regex that "looked right" would reject the first
    // and a length rule would accept a six-character code that is in no list at all.
    private static IReadOnlyList<OffListSegment> FindOffList(
        IReadOnlyList<Document> documents, IReadOnlyList<PicklistItem> picklists)
    {
        if (picklists.Count == 0) return Array.Empty<OffListSegment>();

        var byField = picklists
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.Field)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => p.Code.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var findings = new List<OffListSegment>();
        foreach (var document in documents)
        {
            foreach (var (field, label, value) in Segments(document))
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (!byField.TryGetValue(field, out var codes)) continue;
                if (codes.Contains(value.Trim())) continue;

                findings.Add(new OffListSegment(
                    document.Id, FindingSource.Document, document.Id,
                    document.DocumentNumber, label, value,
                    document.CorporateDiscipline, document.Title));
            }
        }

        return findings;
    }

    // The eight numbering fields and the picklist each is drawn from. Labels name the
    // segment the way the workbook does, so a finding reads as the operator's own column.
    private static IEnumerable<(PicklistField Field, string Label, string Value)> Segments(
        Document d)
    {
        yield return (PicklistField.Project, "Project", d.F01Project);
        yield return (PicklistField.Originator, "Originator", d.F02Originator);
        yield return (PicklistField.Contract, "Contract", d.F03Contract);
        yield return (PicklistField.DocType, "Document type", d.F04DocType);
        yield return (PicklistField.Discipline, "Discipline", d.F05Discipline);
        yield return (PicklistField.Zone, "Zone", d.F06Zone);
        yield return (PicklistField.Building, "Building", d.F07Building);
        yield return (PicklistField.DrawingType, "Drawing type", d.F08ADrawingType);
        yield return (PicklistField.Level, "Level", d.F08BLevel);
    }

    private static DateTime? PlannedStart(
        Document document, IReadOnlyDictionary<Guid, TrackerRow> rowsByDocument) =>
        rowsByDocument.TryGetValue(document.Id, out var row) ? row.PlannedStart : null;

    private static string? Author(Document document) =>
        document.Exchanges.FirstOrDefault(e => e.Number == 1)?.Author;
}
