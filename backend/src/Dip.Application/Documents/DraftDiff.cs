using System.Globalization;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Documents;

// The fields a Draft row is compared against Live on: everything an import writes
// or the Draft editor can change, EXCEPT the eight numbering fields (a change there
// yields a different DocumentNumber, i.e. a New row rather than a Modified one) and
// the data exchanges (compared separately at promote time, where they are loaded).
//
// Anything omitted here would be classified Unchanged and silently dropped by
// Promote, so a new editable column must be added to this record too.
public readonly record struct DraftComparableFields(
    string Title,
    string? ExtractedFromModel,
    string? ScopeArea,
    string? PackageName,
    string? ActivityId,
    string? ClassificationCode,
    string CorporateDiscipline,
    DateTime? DeliveryMilestone,
    string? Scale,
    string? AuthoringSoftware,
    string? ExchangeFormat,
    decimal BudgetWeight);

public sealed record DraftFieldChange(string Field, string? OldValue, string? NewValue);

// Pure Draft-vs-Live comparison shared by the importers (which compute
// DraftRowState while parsing), the Draft editor (which recomputes it after a
// user edit) and PromoteClassifier (which turns it into the promote diff).
public static class DraftDiff
{
    public static DraftComparableFields From(Document live) => new(
        live.Title, live.ExtractedFromModel, live.ScopeArea, live.PackageName, live.ActivityId,
        live.ClassificationCode, live.CorporateDiscipline, live.DeliveryMilestone, live.Scale,
        live.AuthoringSoftware, live.ExchangeFormat, live.BudgetWeight);

    public static DraftComparableFields From(DocumentDraft draft) => new(
        draft.Title, draft.ExtractedFromModel, draft.ScopeArea, draft.PackageName, draft.ActivityId,
        draft.ClassificationCode, draft.CorporateDiscipline, draft.DeliveryMilestone, draft.Scale,
        draft.AuthoringSoftware, draft.ExchangeFormat, draft.BudgetWeight);

    // Ordinal comparison throughout: "STL" and "stl" are different picklist values.
    public static IReadOnlyList<DraftFieldChange> Changes(DraftComparableFields live, DraftComparableFields draft)
    {
        var changes = new List<DraftFieldChange>();
        Add(changes, nameof(Document.Title), live.Title, draft.Title);
        Add(changes, nameof(Document.ExtractedFromModel), live.ExtractedFromModel, draft.ExtractedFromModel);
        Add(changes, nameof(Document.ScopeArea), live.ScopeArea, draft.ScopeArea);
        Add(changes, nameof(Document.PackageName), live.PackageName, draft.PackageName);
        Add(changes, nameof(Document.ActivityId), live.ActivityId, draft.ActivityId);
        Add(changes, nameof(Document.ClassificationCode), live.ClassificationCode, draft.ClassificationCode);
        Add(changes, nameof(Document.CorporateDiscipline), live.CorporateDiscipline, draft.CorporateDiscipline);
        Add(changes, nameof(Document.DeliveryMilestone), Format(live.DeliveryMilestone), Format(draft.DeliveryMilestone));
        Add(changes, nameof(Document.Scale), live.Scale, draft.Scale);
        Add(changes, nameof(Document.AuthoringSoftware), live.AuthoringSoftware, draft.AuthoringSoftware);
        Add(changes, nameof(Document.ExchangeFormat), live.ExchangeFormat, draft.ExchangeFormat);
        Add(changes, nameof(Document.BudgetWeight), Format(live.BudgetWeight), Format(draft.BudgetWeight));
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
    private static string? Format(DateTime? value) => value?.ToString("O", CultureInfo.InvariantCulture);

    private static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
