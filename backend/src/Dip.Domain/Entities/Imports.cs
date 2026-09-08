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
