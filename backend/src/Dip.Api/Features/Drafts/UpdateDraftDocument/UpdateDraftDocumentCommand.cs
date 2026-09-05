using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Drafts.UpdateDraftDocument;

// Full replace of one draft row's editable fields. DocumentNumber is NOT accepted:
// it is always recomposed from F01..F08C (PLAN.md § 5.1.1) so a hand-typed number
// can never disagree with the eight fields it is built from.
//
// Exchanges: null leaves the row's data exchanges untouched; a list replaces them
// (BudgetWeight then follows exchange 01's duration, as on import).
[Permission(Permissions.DraftsEdit)]
public sealed record UpdateDraftDocumentCommand(
    Guid Id,
    string Title,
    string? ExtractedFromModel,
    string? ScopeArea,
    string? AuthoringSoftware,
    string? ExchangeFormat,
    string? Scale,
    DateTime? DeliveryMilestone,
    string? PackageName,
    string? ActivityId,
    string? ClassificationCode,
    string F01Project,
    string F02Originator,
    string F03Contract,
    string F04DocType,
    [property: DisciplineScope] string F05Discipline,
    string F06Zone,
    string F07Building,
    string F08ADrawingType,
    string F08BLevel,
    string F08CSequence,
    string CorporateDiscipline,
    IReadOnlyList<DraftExchangeInput>? Exchanges = null) : ICommand<DraftDocumentDto>;

public sealed record DraftExchangeInput(
    int Number,
    string? Stage,
    string? ProgrammeRef,
    string? Author,
    string? Geometrical,
    string? NonGeometrical,
    int? DurationDays,
    string? Predecessor,
    DateTime? ExchangeDate);
