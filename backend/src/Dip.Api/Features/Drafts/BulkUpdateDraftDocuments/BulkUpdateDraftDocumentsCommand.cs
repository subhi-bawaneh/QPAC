using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Drafts.BulkUpdateDraftDocuments;

// Applies the SAME value to many draft rows — the "select 300 rows, set Package
// Name" gesture of the old Excel workflow. Set-field semantics: a null field is
// left alone (so bulk cannot blank a value; use the single-row PUT for that).
//
// Title and F08CSequence are intentionally absent: they are per-row identity and
// setting them across a selection would only manufacture duplicates.
[Permission(Permissions.DraftsEdit)]
public sealed record BulkUpdateDraftDocumentsCommand(
    IReadOnlyList<Guid> Ids,
    BulkDraftFields Fields) : ICommand<int>;

public sealed record BulkDraftFields(
    string? ExtractedFromModel = null,
    string? ScopeArea = null,
    string? AuthoringSoftware = null,
    string? ExchangeFormat = null,
    string? Scale = null,
    DateTime? DeliveryMilestone = null,
    string? PackageName = null,
    string? ActivityId = null,
    string? ClassificationCode = null,
    string? CorporateDiscipline = null,
    string? F02Originator = null,
    string? F03Contract = null,
    string? F04DocType = null,
    [property: DisciplineScope] string? F05Discipline = null,
    string? F06Zone = null,
    string? F07Building = null,
    string? F08ADrawingType = null,
    string? F08BLevel = null)
{
    public bool HasAny =>
        ExtractedFromModel is not null || ScopeArea is not null || AuthoringSoftware is not null
        || ExchangeFormat is not null || Scale is not null || DeliveryMilestone is not null
        || PackageName is not null || ActivityId is not null || ClassificationCode is not null
        || CorporateDiscipline is not null || F02Originator is not null || F03Contract is not null
        || F04DocType is not null || F05Discipline is not null || F06Zone is not null
        || F07Building is not null || F08ADrawingType is not null || F08BLevel is not null;

    // True when a change can alter DocumentNumber, i.e. the number must be recomposed.
    public bool TouchesNumbering =>
        F02Originator is not null || F03Contract is not null || F04DocType is not null
        || F05Discipline is not null || F06Zone is not null || F07Building is not null
        || F08ADrawingType is not null || F08BLevel is not null;
}
