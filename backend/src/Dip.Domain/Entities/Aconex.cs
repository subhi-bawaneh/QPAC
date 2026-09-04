using Dip.Domain.Common;

namespace Dip.Domain.Entities;

// One row from Aconex History. Uniqueness: (ProjectId, AconexDocNo, Revision, DateModified).
public class AconexRevision : Entity
{
    public Guid ProjectId { get; set; }
    public Guid ImportBatchId { get; set; }

    public string FileType { get; set; } = string.Empty;               // "pdf", "dwg"
    public string FileName { get; set; } = string.Empty;
    public string AconexDocNo { get; set; } = string.Empty;            // raw with spaces & -PDF suffix
    public string DocNoFinal { get; set; } = string.Empty;             // normalized (or "XXX" if not a MIDP shape)
    public string Revision { get; set; } = "00";
    public string Title { get; set; } = string.Empty;
    public string AconexStatus { get; set; } = string.Empty;
    public string? ReviewStatus { get; set; }
    public DateTime DateModified { get; set; }                         // sub-second precision

    public string? Type { get; set; }
    public string? Discipline { get; set; }
    public string? Area { get; set; }
    public string? Venue { get; set; }
    public string? FloorLevel { get; set; }
    public string? TransmittalIn { get; set; }

    // Computed at import time (see docs/excel-analysis.md § 3.2).
    public bool IsTerminated { get; set; }
    public bool IsLatest { get; set; }
    public bool InMidp { get; set; }

    public Project? Project { get; set; }
}
