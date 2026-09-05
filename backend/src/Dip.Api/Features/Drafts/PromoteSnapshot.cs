using System.Globalization;
using System.Text;
using Dip.Domain.Entities;

namespace Dip.Api.Features.Drafts;

// Contents of PromoteBatch.SnapshotJson — the Live state before the promote, kept
// small on purpose: added rows need only their ids (deleting them is the undo),
// while updated and deleted rows carry their full pre-change state.
public sealed record PromoteSnapshot(
    Guid FolderFileId,
    DateTime At,
    IReadOnlyList<Guid> AddedDocumentIds,
    IReadOnlyList<PromotedDocumentState> UpdatedBefore,
    IReadOnlyList<PromotedDocumentState> DeletedBefore,
    Guid? CreatedTidpId);

public sealed record PromotedDocumentState(
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
    DateTime CreatedAt,
    string CreatedBy,
    DateTime UpdatedAt,
    string UpdatedBy,
    IReadOnlyList<PromotedExchangeState> Exchanges)
{
    public static PromotedDocumentState From(Document d) => new(
        d.Id, d.ProjectId, d.TidpId, d.DisciplineId, d.FolderFileId, d.DocumentNumber, d.Title,
        d.ExtractedFromModel, d.ScopeArea, d.AuthoringSoftware, d.ExchangeFormat, d.Scale,
        d.DeliveryMilestone, d.PackageName, d.ActivityId, d.ClassificationCode,
        d.F01Project, d.F02Originator, d.F03Contract, d.F04DocType, d.F05Discipline,
        d.F06Zone, d.F07Building, d.F08ADrawingType, d.F08BLevel, d.F08CSequence,
        d.CorporateDiscipline, d.BudgetWeight,
        d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy,
        d.Exchanges.OrderBy(e => e.Number).Select(PromotedExchangeState.From).ToList());

    // Restores this state onto a Live document (rollback of an Update, or the
    // re-insert of a Deleted row). Exchanges are replaced wholesale.
    public void RestoreTo(Document d)
    {
        d.ProjectId = ProjectId;
        d.TidpId = TidpId;
        d.DisciplineId = DisciplineId;
        d.FolderFileId = FolderFileId;
        d.DocumentNumber = DocumentNumber;
        d.Title = Title;
        d.ExtractedFromModel = ExtractedFromModel;
        d.ScopeArea = ScopeArea;
        d.AuthoringSoftware = AuthoringSoftware;
        d.ExchangeFormat = ExchangeFormat;
        d.Scale = Scale;
        d.DeliveryMilestone = DeliveryMilestone;
        d.PackageName = PackageName;
        d.ActivityId = ActivityId;
        d.ClassificationCode = ClassificationCode;
        d.F01Project = F01Project;
        d.F02Originator = F02Originator;
        d.F03Contract = F03Contract;
        d.F04DocType = F04DocType;
        d.F05Discipline = F05Discipline;
        d.F06Zone = F06Zone;
        d.F07Building = F07Building;
        d.F08ADrawingType = F08ADrawingType;
        d.F08BLevel = F08BLevel;
        d.F08CSequence = F08CSequence;
        d.CorporateDiscipline = CorporateDiscipline;
        d.BudgetWeight = BudgetWeight;
        d.CreatedAt = CreatedAt;
        d.CreatedBy = CreatedBy;
        d.UpdatedAt = UpdatedAt;
        d.UpdatedBy = UpdatedBy;

        // Matched by Number rather than cleared and re-added: (DocumentId, Number) is
        // unique, so replacing the rows wholesale would race its own inserts.
        foreach (var existing in d.Exchanges.ToList())
        {
            if (Exchanges.All(e => e.Number != existing.Number)) d.Exchanges.Remove(existing);
        }

        foreach (var e in Exchanges)
        {
            var target = d.Exchanges.FirstOrDefault(x => x.Number == e.Number);
            if (target is null)
            {
                target = new DataExchange { DocumentId = d.Id, Number = e.Number };
                d.Exchanges.Add(target);
            }

            target.Stage = e.Stage;
            target.ProgrammeRef = e.ProgrammeRef;
            target.Author = e.Author;
            target.Geometrical = e.Geometrical;
            target.NonGeometrical = e.NonGeometrical;
            target.DurationDays = e.DurationDays;
            target.Predecessor = e.Predecessor;
            target.ExchangeDate = e.ExchangeDate;
        }
    }
}

public sealed record PromotedExchangeState(
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
    public static PromotedExchangeState From(DataExchange e) => new(
        e.Number, e.Stage, e.ProgrammeRef, e.Author, e.Geometrical, e.NonGeometrical,
        e.DurationDays, e.Predecessor, e.ExchangeDate);
}

// Canonical rendering of a document's data exchanges, used to detect exchange-only
// changes (a moved exchange date is a real planning change that must promote).
internal static class ExchangeSignature
{
    public static string Of(IEnumerable<DataExchange> exchanges) =>
        Build(exchanges.Select(e => (e.Number, e.Stage, e.ProgrammeRef, e.Author, e.Geometrical,
            e.NonGeometrical, e.DurationDays, e.Predecessor, e.ExchangeDate)));

    public static string Of(IEnumerable<DataExchangeDraft> exchanges) =>
        Build(exchanges.Select(e => (e.Number, e.Stage, e.ProgrammeRef, e.Author, e.Geometrical,
            e.NonGeometrical, e.DurationDays, e.Predecessor, e.ExchangeDate)));

    private static string Build(
        IEnumerable<(int Number, string? Stage, string? ProgrammeRef, string? Author, string? Geometrical,
            string? NonGeometrical, int? DurationDays, string? Predecessor, DateTime? ExchangeDate)> rows)
    {
        var sb = new StringBuilder();
        foreach (var e in rows.OrderBy(e => e.Number))
        {
            sb.Append(e.Number.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(e.Stage).Append('|')
              .Append(e.ProgrammeRef).Append('|')
              .Append(e.Author).Append('|')
              .Append(e.Geometrical).Append('|')
              .Append(e.NonGeometrical).Append('|')
              .Append(e.DurationDays?.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(e.Predecessor).Append('|')
              .Append(e.ExchangeDate?.ToString("O", CultureInfo.InvariantCulture)).Append(';');
        }
        return sb.ToString();
    }
}
