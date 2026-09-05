using System.Globalization;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Documents;

// The set of fields a Draft row is compared against Live on. Kept deliberately
// narrow — these are the planning fields a TIDP/MIDP revision actually changes;
// the eight numbering fields are excluded because a change there produces a
// different DocumentNumber (a New row, not a Modified one).
public readonly record struct DraftComparableFields(
    string Title,
    string? PackageName,
    string? ActivityId,
    string CorporateDiscipline,
    DateTime? DeliveryMilestone,
    string? Scale,
    string? AuthoringSoftware,
    string? ExchangeFormat);

public sealed record DraftFieldChange(string Field, string? OldValue, string? NewValue);

// Pure Draft-vs-Live comparison shared by the importers (which compute
// DraftRowState while parsing) and the Draft editor (which recomputes it after
// a user edit). Phase 4.2's GetPromoteDiff reuses Changes() for its field list.
public static class DraftDiff
{
    public static DraftComparableFields From(Document live) => new(
        live.Title, live.PackageName, live.ActivityId, live.CorporateDiscipline,
        live.DeliveryMilestone, live.Scale, live.AuthoringSoftware, live.ExchangeFormat);

    public static DraftComparableFields From(DocumentDraft draft) => new(
        draft.Title, draft.PackageName, draft.ActivityId, draft.CorporateDiscipline,
        draft.DeliveryMilestone, draft.Scale, draft.AuthoringSoftware, draft.ExchangeFormat);

    // Ordinal comparison throughout: "STL" and "stl" are different picklist values.
    public static IReadOnlyList<DraftFieldChange> Changes(DraftComparableFields live, DraftComparableFields draft)
    {
        var changes = new List<DraftFieldChange>();
        Add(changes, nameof(Document.Title), live.Title, draft.Title);
        Add(changes, nameof(Document.PackageName), live.PackageName, draft.PackageName);
        Add(changes, nameof(Document.ActivityId), live.ActivityId, draft.ActivityId);
        Add(changes, nameof(Document.CorporateDiscipline), live.CorporateDiscipline, draft.CorporateDiscipline);
        Add(changes, nameof(Document.DeliveryMilestone), Format(live.DeliveryMilestone), Format(draft.DeliveryMilestone));
        Add(changes, nameof(Document.Scale), live.Scale, draft.Scale);
        Add(changes, nameof(Document.AuthoringSoftware), live.AuthoringSoftware, draft.AuthoringSoftware);
        Add(changes, nameof(Document.ExchangeFormat), live.ExchangeFormat, draft.ExchangeFormat);
        return changes;
    }

    // No Live counterpart -> New. Otherwise Modified/Unchanged by field comparison.
    public static DraftRowState StateFor(DraftComparableFields? live, DraftComparableFields draft) =>
        live is null
            ? DraftRowState.New
            : Changes(live.Value, draft).Count > 0 ? DraftRowState.Modified : DraftRowState.Unchanged;

    private static void Add(List<DraftFieldChange> changes, string field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            changes.Add(new DraftFieldChange(field, oldValue, newValue));
        }
    }

    // Round-trip format so sub-second precision survives into the diff/audit payload.
    private static string? Format(DateTime? value) =>
        value?.ToString("O", CultureInfo.InvariantCulture);
}
