using Dip.Domain.Common;
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

// One uploaded TIDP workbook — the unit of replacement and deletion. A document
// belongs to exactly one of these, so replacing a file touches only its own rows
// and never a number another file owns (stage 3, first-file-wins).
public class TidpFile : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Guid DisciplineId { get; set; }
    public string DocumentReference { get; set; } = string.Empty;      // "QF01012-NES-C04518-TDP-XX..."
    public string RevisionNumber { get; set; } = "00";
    public DateTime? DateCreated { get; set; }
    public DateTime? DateLastUpdated { get; set; }

    // Upload metadata. The file bytes are never stored: they are parsed and discarded.
    public string FileName { get; set; } = string.Empty;
    public int RowsRead { get; set; }
    public int RowsImported { get; set; }
    public int RowsSkipped { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public TidpFileStatus Status { get; set; } = TidpFileStatus.Importing;
    public string? Error { get; set; }

    // ------------------------------------------------------- folder identity
    // Null for a file uploaded one at a time through /api/projects/{id}/tidp-files.
    // A folder sync fills them in, and RelativePath is then the row's real key: the
    // same FileName lives under three owners in the sample folder, so matching on the
    // name alone would collapse three files into one.
    public string? RelativePath { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? FolderDisciplineId { get; set; }

    // The change detector, in the order it is applied: a differing timestamp or size
    // sends the file to the hasher, and only a differing hash sends it to the importer.
    // A workbook that was opened and saved without an edit is otherwise re-parsed —
    // and a re-parse destroys the hand edits under it.
    public DateTime? LastModifiedUtc { get; set; }
    public long SizeBytes { get; set; }
    public string? ContentHash { get; set; }                           // SHA-256, hex

    public DateTime? LastSeenAt { get; set; }
    public TidpFolderStatus FolderStatus { get; set; } = TidpFolderStatus.Present;
    public DateTime? MissingSince { get; set; }

    // The eight fields of the file's own name. The workbook is not a reliable source
    // for these — most sample files carry the placeholder `...-TDP-XXX-00-000000-000001`
    // in DOCUMENT REFERENCE — so the name is parsed and kept. Strings throughout:
    // Zone "00", Level "000000" and Sequence "000001" all lose meaning as numbers.
    public string? NameProjectCode { get; set; }
    public string? NameOriginator { get; set; }
    public string? NameContract { get; set; }
    public string? NameDocType { get; set; }
    public string? DisciplineTag { get; set; }                         // "ARC", "STL", "KNL"
    public string? NameZone { get; set; }
    public string? NameLevel { get; set; }
    public string? Sequence { get; set; }                              // "000001", "19000"

    public Project? Project { get; set; }
    public Discipline? Discipline { get; set; }
    public TidpFolderOwner? Owner { get; set; }
    public TidpFolderDiscipline? FolderDiscipline { get; set; }
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}

// One planned document (row in a TIDP sheet).
public class Document : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Guid TidpFileId { get; set; }
    public Guid DisciplineId { get; set; }

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
    // The number itself is the sheet's own DOCUMENT NUMBER, normalised; these fields are
    // what the editor recomposes from and what the off-list findings check.
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

    // Set the moment a person changes any field. A replace destroys these rows, so the
    // count of them is what the confirmation dialog shows before it does (stage 3).
    public bool IsEdited { get; set; }
    public string? EditedBy { get; set; }
    public DateTime? EditedAt { get; set; }

    public Project? Project { get; set; }
    public TidpFile? TidpFile { get; set; }
    public Discipline? Discipline { get; set; }
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
