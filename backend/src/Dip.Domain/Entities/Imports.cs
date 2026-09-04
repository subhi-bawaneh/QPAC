using Dip.Domain.Common;
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public class ImportBatch : Entity
{
    public Guid ProjectId { get; set; }
    public ImportKind Kind { get; set; }
    public DataTarget Target { get; set; } = DataTarget.Live;
    public Guid? FolderFileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public string ImportedBy { get; set; } = string.Empty;
    public int RowsRead { get; set; }
    public int RowsInserted { get; set; }
    public int RowsUpdated { get; set; }
    public int RowsSkipped { get; set; }
    public string? Log { get; set; }                                   // JSON — warnings/errors
    public bool Completed { get; set; }

    public Project? Project { get; set; }
    public FolderFile? FolderFile { get; set; }
}

// Chunked-import staging: one row per parsed excel row, kept until the whole file
// is materialized. Cleaned up after Completed. Enables resume-on-error and progress bars.
public class ImportStagingRow : Entity
{
    public Guid ImportBatchId { get; set; }
    public int RowNumber { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public bool Processed { get; set; }
    public string? Error { get; set; }
}
