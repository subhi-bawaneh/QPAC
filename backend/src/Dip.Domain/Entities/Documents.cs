using Dip.Domain.Common;

namespace Dip.Domain.Entities;

// A TIDP file — one per discipline. Corresponds to a row in the Folders/Files tree.
public class Tidp : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Guid DisciplineId { get; set; }
    public Guid? FolderFileId { get; set; }
    public string DocumentReference { get; set; } = string.Empty;      // "QF01012-NES-C04518-TDP-XX..."
    public string RevisionNumber { get; set; } = "00";
    public DateTime? DateCreated { get; set; }
    public DateTime? DateLastUpdated { get; set; }
    public string? SourceFileName { get; set; }

    public Project? Project { get; set; }
    public Discipline? Discipline { get; set; }
    public FolderFile? FolderFile { get; set; }
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}

// One planned document (row in TIDP_Sheet / MIDP).
public class Document : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Guid TidpId { get; set; }
    public Guid DisciplineId { get; set; }
    public Guid? FolderFileId { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;         // UNIQUE (ProjectId, DocumentNumber)
    public string Title { get; set; } = string.Empty;
    public string? ExtractedFromModel { get; set; }
    public string? ScopeArea { get; set; }
    public string? AuthoringSoftware { get; set; }
    public string? ExchangeFormat { get; set; }
    public string? Scale { get; set; }
    public DateTime? DeliveryMilestone { get; set; }
    public string? PackageName { get; set; }
    public string? ActivityId { get; set; }                             // Baseline key
    public string? ClassificationCode { get; set; }

    // 8-field numbering scheme; strings so leading zeros survive (Zone "00", Sequence "0004").
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

    public string CorporateDiscipline { get; set; } = string.Empty;    // "Structural" — drives report grouping

    // Weight used by SPI (§ 7). Default = Exchange 01 duration (or 1 if missing).
    public decimal BudgetWeight { get; set; } = 1m;

    public Project? Project { get; set; }
    public Tidp? Tidp { get; set; }
    public Discipline? Discipline { get; set; }
    public FolderFile? FolderFile { get; set; }
    public ICollection<DataExchange> Exchanges { get; set; } = new List<DataExchange>();
}

public class DataExchange : Entity
{
    public Guid DocumentId { get; set; }
    public int Number { get; set; }                                    // 1 or 2
    public string? Stage { get; set; }                                 // e.g. "HAND OVER"
    public string? ProgrammeRef { get; set; }
    public string? Author { get; set; }
    public string? Geometrical { get; set; }                           // "LOD400"
    public string? NonGeometrical { get; set; }
    public int? DurationDays { get; set; }
    public string? Predecessor { get; set; }
    public DateTime? ExchangeDate { get; set; }

    public Document? Document { get; set; }
}
