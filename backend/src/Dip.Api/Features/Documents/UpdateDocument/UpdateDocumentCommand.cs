using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Documents.UpdateDocument;

// The Live-layer twin of UpdateDraftDocumentCommand: same editable fields, same
// derived DocumentNumber, but it writes the Live tables and leaves an AuditLog row
// per changed field.
[Permission(Permissions.DocumentsEdit)]
public sealed record UpdateDocumentCommand(
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
    IReadOnlyList<ExchangeInput>? Exchanges = null) : ICommand<DocumentDto>;
