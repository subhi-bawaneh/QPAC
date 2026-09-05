using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Drafts;

// Wire shape for a Draft row. DocumentNumber is derived from F01..F08C and is
// therefore read-only for clients — see UpdateDraftDocumentCommand.
public sealed record DraftDocumentDto(
    Guid Id,
    Guid ProjectId,
    Guid TidpDraftId,
    Guid DisciplineId,
    Guid FolderFileId,
    Guid ImportBatchId,
    string DocumentNumber,
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
    string F05Discipline,
    string F06Zone,
    string F07Building,
    string F08ADrawingType,
    string F08BLevel,
    string F08CSequence,
    string CorporateDiscipline,
    decimal BudgetWeight,
    DraftRowState State,
    Guid? LiveDocumentId,
    string? ConflictReason,
    bool IsDuplicate,
    DateTime UpdatedAt,
    string UpdatedBy,
    IReadOnlyList<DraftExchangeDto> Exchanges)
{
    public static DraftDocumentDto From(DocumentDraft d) => new(
        d.Id, d.ProjectId, d.TidpDraftId, d.DisciplineId, d.FolderFileId, d.ImportBatchId,
        d.DocumentNumber, d.Title, d.ExtractedFromModel, d.ScopeArea, d.AuthoringSoftware,
        d.ExchangeFormat, d.Scale, d.DeliveryMilestone, d.PackageName, d.ActivityId,
        d.ClassificationCode,
        d.F01Project, d.F02Originator, d.F03Contract, d.F04DocType, d.F05Discipline,
        d.F06Zone, d.F07Building, d.F08ADrawingType, d.F08BLevel, d.F08CSequence,
        d.CorporateDiscipline, d.BudgetWeight,
        d.State, d.LiveDocumentId, d.ConflictReason, d.IsDuplicate,
        d.UpdatedAt, d.UpdatedBy,
        d.Exchanges.OrderBy(e => e.Number).Select(DraftExchangeDto.From).ToList());
}

public sealed record DraftExchangeDto(
    int Number,
    string? Stage,
    string? ProgrammeRef,
    string? Author,
    string? Geometrical,
    string? NonGeometrical,
    int? DurationDays,
    string? Predecessor,
    DateTime? ExchangeDate)
{
    public static DraftExchangeDto From(DataExchangeDraft e) => new(
        e.Number, e.Stage, e.ProgrammeRef, e.Author, e.Geometrical, e.NonGeometrical,
        e.DurationDays, e.Predecessor, e.ExchangeDate);
}
