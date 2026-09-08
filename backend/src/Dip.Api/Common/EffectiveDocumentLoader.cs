using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Common;

// One row of the effective document set (refactor-plan § 3 R8). For a Live row the
// Document is the stored entity; for a Draft row it is an in-memory projection of the
// DocumentDraft, so every engine and report can stay written against Document alone.
internal sealed record EffectiveDocument(
    Document Document,
    DataTarget Layer,
    Guid RowId,
    Guid? FolderFileId,
    DateTime SourceModifiedAt);

// What the Tracker and every report read: the Live rows of Live-target files plus the
// Draft rows of Draft-target files, deduplicated by document number with the newest
// file winning and Live breaking a tie.
//
// Some companies work in Drive (Draft) and others in the system (Live), so a report
// that read only one layer would silently omit half the project.
internal static class EffectiveDocumentLoader
{
    public static async Task<IReadOnlyList<EffectiveDocument>> LoadAsync(
        DipDbContext db, Guid projectId, CancellationToken ct)
    {
        var files = await db.FolderFiles
            .AsNoTracking()
            .Where(f => !f.IsDeleted && f.Folder!.ProjectId == projectId && !f.Folder.IsDeleted)
            .Select(f => new { f.Id, f.Folder!.Target, f.ContentModifiedAt })
            .ToListAsync(ct);

        var modifiedByFile = files.ToDictionary(f => f.Id, f => f.ContentModifiedAt);

        var liveFileIds = files.Where(f => f.Target == DataTarget.Live).Select(f => f.Id).ToList();
        var draftFileIds = files.Where(f => f.Target == DataTarget.Draft).Select(f => f.Id).ToList();

        // Live rows with no file at all (hand-created documents) belong to the Live layer.
        var live = await db.Documents
            .AsNoTracking()
            .Include(d => d.Exchanges)
            .Where(d => d.ProjectId == projectId
                && (d.FolderFileId == null || liveFileIds.Contains(d.FolderFileId.Value)))
            .ToListAsync(ct);

        var drafts = await db.DocumentDrafts
            .AsNoTracking()
            .Include(d => d.Exchanges)
            .Where(d => d.ProjectId == projectId && !d.IsDuplicate && draftFileIds.Contains(d.FolderFileId))
            .ToListAsync(ct);

        var candidates = new List<EffectiveDocument>(live.Count + drafts.Count);

        foreach (var document in live)
        {
            candidates.Add(new EffectiveDocument(
                document,
                DataTarget.Live,
                document.Id,
                document.FolderFileId,
                Modified(modifiedByFile, document.FolderFileId)));
        }

        foreach (var draft in drafts)
        {
            candidates.Add(new EffectiveDocument(
                ToDocument(draft),
                DataTarget.Draft,
                draft.Id,
                draft.FolderFileId,
                Modified(modifiedByFile, draft.FolderFileId)));
        }

        return Deduplicate(candidates);
    }

    public static async Task<IReadOnlyList<EffectiveDocument>> LoadPageAsync(
        DipDbContext db, Guid projectId, int offset, int take, CancellationToken ct)
    {
        // The dedupe is a whole-project decision, so the set is built first and paged
        // in memory. At ~20k rows for QPAC that is a fraction of a second.
        var all = await LoadAsync(db, projectId, ct);
        return all.OrderBy(d => d.RowId).Skip(offset).Take(take).ToList();
    }

    private static DateTime Modified(IReadOnlyDictionary<Guid, DateTime> modifiedByFile, Guid? fileId) =>
        fileId is not null && modifiedByFile.TryGetValue(fileId.Value, out var at)
            ? at
            : DateTime.MinValue;

    // R8: newest source file wins; Live wins a tie.
    private static List<EffectiveDocument> Deduplicate(List<EffectiveDocument> candidates)
    {
        var winners = new Dictionary<string, EffectiveDocument>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var key = candidate.Document.DocumentNumber.ToUpperInvariant();
            if (!winners.TryGetValue(key, out var current))
            {
                winners[key] = candidate;
                continue;
            }

            if (Beats(candidate, current)) winners[key] = candidate;
        }

        return winners.Values.ToList();
    }

    private static bool Beats(EffectiveDocument candidate, EffectiveDocument current)
    {
        if (candidate.SourceModifiedAt != current.SourceModifiedAt)
        {
            return candidate.SourceModifiedAt > current.SourceModifiedAt;
        }

        return candidate.Layer == DataTarget.Live && current.Layer == DataTarget.Draft;
    }

    // Draft rows carry the same columns; projecting them keeps a single engine input type.
    private static Document ToDocument(DocumentDraft draft) => new()
    {
        Id = draft.Id,
        ProjectId = draft.ProjectId,
        DisciplineId = draft.DisciplineId,
        FolderFileId = draft.FolderFileId,
        DocumentNumber = draft.DocumentNumber,
        Title = draft.Title,
        ExtractedFromModel = draft.ExtractedFromModel,
        ScopeArea = draft.ScopeArea,
        AuthoringSoftware = draft.AuthoringSoftware,
        ExchangeFormat = draft.ExchangeFormat,
        Scale = draft.Scale,
        DeliveryMilestone = draft.DeliveryMilestone,
        PackageName = draft.PackageName,
        ActivityId = draft.ActivityId,
        ClassificationCode = draft.ClassificationCode,
        F01Project = draft.F01Project,
        F02Originator = draft.F02Originator,
        F03Contract = draft.F03Contract,
        F04DocType = draft.F04DocType,
        F05Discipline = draft.F05Discipline,
        F06Zone = draft.F06Zone,
        F07Building = draft.F07Building,
        F08ADrawingType = draft.F08ADrawingType,
        F08BLevel = draft.F08BLevel,
        F08CSequence = draft.F08CSequence,
        CorporateDiscipline = draft.CorporateDiscipline,
        BudgetWeight = draft.BudgetWeight,
        CreatedAt = draft.CreatedAt,
        CreatedBy = draft.CreatedBy,
        UpdatedAt = draft.UpdatedAt,
        UpdatedBy = draft.UpdatedBy,
        Exchanges = draft.Exchanges
            .OrderBy(e => e.Number)
            .Select(e => new DataExchange
            {
                DocumentId = draft.Id,
                Number = e.Number,
                Stage = e.Stage,
                ProgrammeRef = e.ProgrammeRef,
                Author = e.Author,
                Geometrical = e.Geometrical,
                NonGeometrical = e.NonGeometrical,
                DurationDays = e.DurationDays,
                Predecessor = e.Predecessor,
                ExchangeDate = e.ExchangeDate,
            })
            .ToList(),
    };
}
