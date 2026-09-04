using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

// Materialized per-document engine output — rebuilt after every Live import or Promote.
// Uses DocumentId as primary key (one snapshot per document) instead of a surrogate.
public class DocumentSnapshot
{
    public Guid DocumentId { get; set; }
    public Guid ProjectId { get; set; }
    public DateTime ComputedAt { get; set; }

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

    public Document? Document { get; set; }
    public Project? Project { get; set; }
}
