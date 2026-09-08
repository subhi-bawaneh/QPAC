using Dip.Domain.Common;
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

// Draft mirrors of Tidp/Document/DataExchange used while a folder's Target = Draft.
// Full-copy fields keep engine logic simple: the same importer/validator code runs against Draft.
// On Promote, matching rows are upserted into the Live tables under a transaction.

public class TidpDraft : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Guid DisciplineId { get; set; }
    public Guid FolderFileId { get; set; }
    public Guid ImportBatchId { get; set; }
    public string DocumentReference { get; set; } = string.Empty;
    public string RevisionNumber { get; set; } = "00";
    public DateTime? DateCreated { get; set; }
    public DateTime? DateLastUpdated { get; set; }
    public string? SourceFileName { get; set; }
    public DraftRowState State { get; set; } = DraftRowState.New;

    public ICollection<DocumentDraft> Documents { get; set; } = new List<DocumentDraft>();
}

public class DocumentDraft : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Guid TidpDraftId { get; set; }
    public Guid DisciplineId { get; set; }
    public Guid FolderFileId { get; set; }
    public Guid ImportBatchId { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ExtractedFromModel { get; set; }
    public string? ScopeArea { get; set; }
    public string? AuthoringSoftware { get; set; }
    public string? ExchangeFormat { get; set; }
    public string? Scale { get; set; }
    public DateTime? DeliveryMilestone { get; set; }
    public string? PackageName { get; set; }
    public string? ActivityId { get; set; }
    public string? ClassificationCode { get; set; }

    public string F01Project { get; set; } = string.Empty;
    public string F02Originator { get; set; } = string.Empty;
    public string F03Contract { get; set; } = string.Empty;
    public string F04DocType { get; set; } = string.Empty;
    public string F05Discipline { get; set; } = string.Empty;
    public string F06Zone { get; set; } = string.Empty;
    public string F07Building { get; set; } = string.Empty;
    public string F08ADrawingType { get; set; } = string.Empty;
    public string F08BLevel { get; set; } = string.Empty;
    public string F08CSequence { get; set; } = string.Empty;

    public string CorporateDiscipline { get; set; } = string.Empty;
    public decimal BudgetWeight { get; set; } = 1m;

    public DraftRowState State { get; set; } = DraftRowState.New;
    public Guid? LiveDocumentId { get; set; }
    public string? ConflictReason { get; set; }
    public bool IsDuplicate { get; set; }

    public ICollection<DataExchangeDraft> Exchanges { get; set; } = new List<DataExchangeDraft>();
}

public class DataExchangeDraft : Entity
{
    public Guid DocumentDraftId { get; set; }
    public int Number { get; set; }
    public string? Stage { get; set; }
    public string? ProgrammeRef { get; set; }
    public string? Author { get; set; }
    public string? Geometrical { get; set; }
    public string? NonGeometrical { get; set; }
    public int? DurationDays { get; set; }
    public string? Predecessor { get; set; }
    public DateTime? ExchangeDate { get; set; }
}

// Records a Promote operation for audit. There is no rollback (decision D7) —
// AuditLog carries the field-level undo trail.
public class PromoteBatch : Entity
{
    public Guid ProjectId { get; set; }
    public Guid FolderId { get; set; }
    public Guid FolderFileId { get; set; }
    public DateTime At { get; set; }
    public string By { get; set; } = string.Empty;
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }
    public int Skipped { get; set; }
}
