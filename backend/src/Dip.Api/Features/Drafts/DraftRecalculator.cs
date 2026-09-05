using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Drafts;

// Keeps the three derived columns of a Draft row consistent after a manual edit:
//   DocumentNumber  — recomposed from F01..F08C (never typed by the user)
//   IsDuplicate     — first occurrence within the file wins, the rest are flagged
//   State/LiveDocumentId — recomputed against Live via DraftDiff
//
// Only the affected number groups are touched, not the whole file: a MIDP draft
// holds ~16k rows and an edit must not rewrite all of them.
internal static class DraftRecalculator
{
    // Normalizes the padded fields, writes them back, and recomposes the number.
    // Returns the number the row had before the edit.
    public static string ApplyNumber(DocumentDraft draft)
    {
        var previous = draft.DocumentNumber;
        draft.F06Zone = DocumentNumbering.NormalizeZone(draft.F06Zone);
        draft.F08CSequence = DocumentNumbering.NormalizeSequence(draft.F08CSequence);
        draft.DocumentNumber = DocumentNumbering.Compose(
            draft.F01Project, draft.F02Originator, draft.F03Contract, draft.F04DocType,
            draft.F05Discipline, draft.F06Zone, draft.F07Building,
            draft.F08ADrawingType, draft.F08BLevel, draft.F08CSequence);
        return previous;
    }

    // `previousNumbers` are the numbers the touched rows carried before the edit —
    // their old groups need re-flagging too (removing a row can un-duplicate a sibling).
    public static async Task RefreshAsync(
        DipDbContext db,
        Guid projectId,
        Guid folderFileId,
        IReadOnlyCollection<DocumentDraft> touched,
        IEnumerable<string> previousNumbers,
        CancellationToken ct,
        IReadOnlySet<Guid>? ignoreLiveDocumentIds = null)
    {
        if (touched.Count == 0) return;

        // ---- State + LiveDocumentId for the edited rows only.
        var currentUpper = touched
            .Select(d => d.DocumentNumber.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var live = await db.Documents
            .Where(d => d.ProjectId == projectId && currentUpper.Contains(d.DocumentNumber.ToUpper()))
            .ToListAsync(ct);
        // A rollback has Live rows pending deletion in the change tracker; EF still
        // returns them from a tracking query, so the caller names them to be ignored.
        var liveByNumber = live
            .Where(d => ignoreLiveDocumentIds is null || !ignoreLiveDocumentIds.Contains(d.Id))
            .GroupBy(d => d.DocumentNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var draft in touched)
        {
            if (liveByNumber.TryGetValue(draft.DocumentNumber, out var liveDoc))
            {
                draft.State = DraftDiff.StateFor(DraftDiff.From(liveDoc), DraftDiff.From(draft));
                draft.LiveDocumentId = liveDoc.Id;
            }
            else
            {
                draft.State = DraftDiff.StateFor(null, DraftDiff.From(draft));
                draft.LiveDocumentId = null;
            }
        }

        // ---- Duplicate flags for the old and new groups.
        var affectedUpper = currentUpper
            .Concat(previousNumbers.Select(n => n.ToUpperInvariant()))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // The predicate runs against the *stored* numbers, so an edited row is found
        // under its old number; the in-memory (tracked) instances carry the new one.
        var stored = await db.DocumentDrafts
            .Where(d => d.FolderFileId == folderFileId && affectedUpper.Contains(d.DocumentNumber.ToUpper()))
            .ToListAsync(ct);

        var candidates = stored
            .Concat(touched)
            .DistinctBy(d => d.Id)
            .Where(d => d.FolderFileId == folderFileId);

        foreach (var group in candidates.GroupBy(d => d.DocumentNumber, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(d => d.CreatedAt).ThenBy(d => d.Id).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].IsDuplicate = i > 0;
            }
        }
    }
}
