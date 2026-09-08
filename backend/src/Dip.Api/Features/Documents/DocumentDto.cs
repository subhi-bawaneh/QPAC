using Dip.Domain.Entities;

namespace Dip.Api.Features.Documents;

// Wire shape for a Live document row — the same editable fields as a draft row, minus
// the draft-only bookkeeping. DocumentNumber is derived from F01..F08C.
public sealed record DocumentDto(
    Guid Id,
    Guid ProjectId,
    Guid TidpId,
    Guid DisciplineId,
    Guid? FolderFileId,
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
    DateTime UpdatedAt,
    string UpdatedBy,
    IReadOnlyList<DocumentExchangeDto> Exchanges)
{
    public static DocumentDto From(Document d) => new(
        d.Id, d.ProjectId, d.TidpId, d.DisciplineId, d.FolderFileId,
        d.DocumentNumber, d.Title, d.ExtractedFromModel, d.ScopeArea, d.AuthoringSoftware,
        d.ExchangeFormat, d.Scale, d.DeliveryMilestone, d.PackageName, d.ActivityId,
        d.ClassificationCode,
        d.F01Project, d.F02Originator, d.F03Contract, d.F04DocType, d.F05Discipline,
        d.F06Zone, d.F07Building, d.F08ADrawingType, d.F08BLevel, d.F08CSequence,
        d.CorporateDiscipline, d.BudgetWeight, d.UpdatedAt, d.UpdatedBy,
        d.Exchanges.OrderBy(e => e.Number).Select(DocumentExchangeDto.From).ToList());
}

public sealed record DocumentExchangeDto(
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
    public static DocumentExchangeDto From(DataExchange e) => new(
        e.Number, e.Stage, e.ProgrammeRef, e.Author, e.Geometrical, e.NonGeometrical,
        e.DurationDays, e.Predecessor, e.ExchangeDate);
}
