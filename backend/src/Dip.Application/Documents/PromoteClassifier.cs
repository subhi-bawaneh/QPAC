namespace Dip.Application.Documents;

// What Promote will do with one row (PLAN.md § 3.4).
public enum PromoteAction
{
    Add = 0,        // no Live row with this number
    Update = 1,     // Live row exists and differs
    Unchanged = 2,  // Live row exists and is identical — nothing to write
    Delete = 3,     // Live row from THIS file that the draft no longer contains
    Conflict = 4,   // must not be written; see Reason
}

public sealed record PromoteDecision(
    PromoteAction Action,
    string DocumentNumber,
    Guid? DraftId,
    Guid? LiveId,
    IReadOnlyList<DraftFieldChange> Changes,
    string? Reason);

// One draft row, reduced to what the classification needs.
// ExchangeSignature is a canonical rendering of the row's data exchanges; they are
// compared here rather than inside DraftDiff because only the promote path loads them.
public sealed record PromoteDraftRow(
    Guid Id,
    string DocumentNumber,
    bool IsDuplicate,
    DraftComparableFields Fields,
    string ExchangeSignature);

public sealed record PromoteLiveRow(
    Guid Id,
    string DocumentNumber,
    Guid? FolderFileId,
    DateTime UpdatedAt,
    DraftComparableFields Fields,
    string ExchangeSignature);

// Pure classification of a Draft file against the Live layer — no DbContext, so the
// promote rules are unit-testable on their own. PLAN.md § 3.4:
//   Added     — no Live row with that number
//   Modified  — Live row exists and any promotable field differs
//   Deleted   — Live row from THIS file that the draft no longer contains
//   Conflicts — Live row owned by ANOTHER file, or Live changed after the baseline
//               (the draft's import, or this file's own last promote if that is later)
public static class PromoteClassifier
{
    public const string DuplicateReason = "Duplicate document number within this file";
    public const string OtherFileReason = "A Live document with this number came from another file";
    public const string ChangedSinceImportReason = "The Live document changed after this draft was imported";

    public static IReadOnlyList<PromoteDecision> Classify(
        IEnumerable<PromoteDraftRow> draftRows,
        IEnumerable<PromoteLiveRow> liveRows,
        Guid folderFileId,
        DateTime baseline)
    {
        var liveByNumber = liveRows
            .GroupBy(r => r.DocumentNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var decisions = new List<PromoteDecision>();
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var draft in draftRows)
        {
            seenNumbers.Add(draft.DocumentNumber);

            // A duplicate inside the file cannot be written: (ProjectId, DocumentNumber)
            // is unique on the Live table, so the second row would fail the insert.
            if (draft.IsDuplicate)
            {
                decisions.Add(new PromoteDecision(
                    PromoteAction.Conflict, draft.DocumentNumber, draft.Id, null,
                    Array.Empty<DraftFieldChange>(), DuplicateReason));
                continue;
            }

            if (!liveByNumber.TryGetValue(draft.DocumentNumber, out var live))
            {
                decisions.Add(new PromoteDecision(
                    PromoteAction.Add, draft.DocumentNumber, draft.Id, null,
                    Array.Empty<DraftFieldChange>(), null));
                continue;
            }

            if (live.FolderFileId is not null && live.FolderFileId != folderFileId)
            {
                decisions.Add(new PromoteDecision(
                    PromoteAction.Conflict, draft.DocumentNumber, draft.Id, live.Id,
                    Array.Empty<DraftFieldChange>(), OtherFileReason));
                continue;
            }

            if (live.UpdatedAt > baseline)
            {
                decisions.Add(new PromoteDecision(
                    PromoteAction.Conflict, draft.DocumentNumber, draft.Id, live.Id,
                    Array.Empty<DraftFieldChange>(), ChangedSinceImportReason));
                continue;
            }

            var changes = DraftDiff.Changes(live.Fields, draft.Fields);
            var exchangesDiffer = !string.Equals(
                live.ExchangeSignature, draft.ExchangeSignature, StringComparison.Ordinal);

            if (changes.Count == 0 && !exchangesDiffer)
            {
                decisions.Add(new PromoteDecision(
                    PromoteAction.Unchanged, draft.DocumentNumber, draft.Id, live.Id,
                    Array.Empty<DraftFieldChange>(), null));
                continue;
            }

            if (exchangesDiffer)
            {
                changes = changes.Append(new DraftFieldChange(
                    "Exchanges", live.ExchangeSignature, draft.ExchangeSignature)).ToList();
            }

            decisions.Add(new PromoteDecision(
                PromoteAction.Update, draft.DocumentNumber, draft.Id, live.Id, changes, null));
        }

        // Live rows this file owns that the draft no longer lists. Always reported;
        // only actually removed when the user promotes with deleteMissing.
        foreach (var live in liveByNumber.Values)
        {
            if (live.FolderFileId == folderFileId && !seenNumbers.Contains(live.DocumentNumber))
            {
                decisions.Add(new PromoteDecision(
                    PromoteAction.Delete, live.DocumentNumber, null, live.Id,
                    Array.Empty<DraftFieldChange>(), null));
            }
        }

        return decisions;
    }
}
