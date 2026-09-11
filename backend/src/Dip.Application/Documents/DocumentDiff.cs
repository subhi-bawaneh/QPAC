using System.Globalization;
using Dip.Domain.Entities;

namespace Dip.Application.Documents;

// The fields an edit is compared on: everything an import writes or a person can
// change, EXCEPT the eight numbering fields (handled separately, because a change
// there recomposes the number) and the data exchanges (replaced wholesale).
//
// Anything omitted here is silently missing from the audit trail, so a new
// editable column must be added to this record too.
public readonly record struct ComparableFields(
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

public sealed record FieldChange(string Field, string? OldValue, string? NewValue);

// Pure before/after comparison. Used by the document editor to write one AuditLog
// row per changed field, which is what makes the audit log the undo trail the
// refactor plan promised when it deleted rollback.
public static class DocumentDiff
{
    public static ComparableFields From(Document document) => new(
        document.Title, document.ExtractedFromModel, document.ScopeArea, document.PackageName,
        document.ActivityId, document.ClassificationCode, document.CorporateDiscipline,
        document.DeliveryMilestone, document.Scale, document.AuthoringSoftware,
        document.ExchangeFormat, document.BudgetWeight);

    // Ordinal comparison throughout: "STL" and "stl" are different picklist values.
    public static IReadOnlyList<FieldChange> Changes(ComparableFields before, ComparableFields after)
    {
        var changes = new List<FieldChange>();
        Add(changes, nameof(Document.Title), before.Title, after.Title);
        Add(changes, nameof(Document.ExtractedFromModel), before.ExtractedFromModel, after.ExtractedFromModel);
        Add(changes, nameof(Document.ScopeArea), before.ScopeArea, after.ScopeArea);
        Add(changes, nameof(Document.PackageName), before.PackageName, after.PackageName);
        Add(changes, nameof(Document.ActivityId), before.ActivityId, after.ActivityId);
        Add(changes, nameof(Document.ClassificationCode), before.ClassificationCode, after.ClassificationCode);
        Add(changes, nameof(Document.CorporateDiscipline), before.CorporateDiscipline, after.CorporateDiscipline);
        Add(changes, nameof(Document.DeliveryMilestone), Format(before.DeliveryMilestone), Format(after.DeliveryMilestone));
        Add(changes, nameof(Document.Scale), before.Scale, after.Scale);
        Add(changes, nameof(Document.AuthoringSoftware), before.AuthoringSoftware, after.AuthoringSoftware);
        Add(changes, nameof(Document.ExchangeFormat), before.ExchangeFormat, after.ExchangeFormat);
        Add(changes, nameof(Document.BudgetWeight), Format(before.BudgetWeight), Format(after.BudgetWeight));
        return changes;
    }

    private static void Add(List<FieldChange> changes, string field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            changes.Add(new FieldChange(field, oldValue, newValue));
        }
    }

    // Round-trip format so sub-second precision survives into the audit payload.
    private static string? Format(DateTime? value) => value?.ToString("O", CultureInfo.InvariantCulture);

    private static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
