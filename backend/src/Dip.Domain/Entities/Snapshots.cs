using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

// The materialised tracker row (decision D11): one per row of the effective
// document set, carrying both the display columns copied from the source row and
// the TrackerEngine output. Keyed by the source row's id — a Document.Id when
// Layer = Live, a DocumentDraft.Id when Layer = Draft — with no foreign key,
// because the two layers live in different tables. Rebuilt by RecalculationService.
public class DocumentSnapshot
{
    public Guid DocumentId { get; set; }
    public Guid ProjectId { get; set; }
    public DataTarget Layer { get; set; }
    public Guid? FolderFileId { get; set; }
    public DateTime ComputedAt { get; set; }

    // Display columns — copied so the Tracker grid never joins the source tables.
    public string DocumentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;                   // F04DocType
    public string Discipline { get; set; } = string.Empty;             // CorporateDiscipline
    public string Building { get; set; } = string.Empty;               // F07Building
    public string Level { get; set; } = string.Empty;                  // F08BLevel
    public string Trade { get; set; } = string.Empty;                  // F05Discipline
    public string? Author { get; set; }                                // Exchange 01 author
    public DateTime? DeliveryMilestone { get; set; }
    public string? ActivityId { get; set; }
    public string? PackageName { get; set; }

    // Computed columns (TrackerEngine).
    public int? SubmissionsCount { get; set; }
    public string? Revision { get; set; }
    public string? AconexStatus { get; set; }
    public UnifiedStatus? Status { get; set; }
    public DateTime? SubmissionDate { get; set; }
    public DateTime? DateModified { get; set; }
    public string? Transmittal { get; set; }

    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedFinish { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualFinish { get; set; }

    public Project? Project { get; set; }
}
