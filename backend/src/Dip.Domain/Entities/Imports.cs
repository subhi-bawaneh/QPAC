using Dip.Domain.Common;
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

// One row per upload. The only record of what was loaded when: the file bytes are
// discarded after a successful import, so this row and the AuditLogs are all that
// survive it.
public class ImportBatch : Entity
{
    public Guid ProjectId { get; set; }
    public ImportKind Kind { get; set; }
    public Guid? TidpFileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public int RowsRead { get; set; }
    public int RowsInserted { get; set; }
    public int RowsUpdated { get; set; }
    public int RowsSkipped { get; set; }

    // Aconex appends drop lines already held. Counted here so the operator can see
    // that a re-upload of an overlapping export did nothing, which is the point.
    public int RowsDuplicate { get; set; }

    public string? Log { get; set; }                                   // JSON — per-importer counters, warnings, error
    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Queued;

    public Project? Project { get; set; }
    public TidpFile? TidpFile { get; set; }
}
