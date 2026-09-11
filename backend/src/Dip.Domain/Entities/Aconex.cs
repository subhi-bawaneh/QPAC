using Dip.Domain.Common;

namespace Dip.Domain.Entities;

// One row from Aconex History.
//
// Identity is LineHash — a hash of the whole normalised source line — because an
// upload appends what it does not already hold, and Aconex can re-export the same
// event with a corrected title. The old (AconexDocNo, Revision, DateModified) key
// would have refused that second line; it survives as a plain index because the flag
// computation and the tracker both group on it.
public class AconexRevision : Entity
{
    public Guid ProjectId { get; set; }
    public Guid ImportBatchId { get; set; }

    // SHA-256 of the normalised source columns. UNIQUE (ProjectId, LineHash).
    public string LineHash { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;               // "pdf", "dwg"
    public string FileName { get; set; } = string.Empty;
    public string AconexDocNo { get; set; } = string.Empty;            // raw with spaces & -PDF suffix
    // Null when the raw Aconex value is not a document number at all — the absence of
    // a number, said plainly, rather than a sentinel every reader has to know about.
    public string? DocNoFinal { get; set; }
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

    // Recomputed over the whole surviving set after every append, never over the new
    // rows alone: both are cross-row aggregates, so an incremental pass would leave a
    // drawing with two rows claiming to be the latest.
    public bool IsTerminated { get; set; }
    public bool IsLatest { get; set; }

    public Project? Project { get; set; }
}
