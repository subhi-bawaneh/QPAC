using Dip.Domain.Common;
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public class Folder : Entity
{
    public Guid ProjectId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;                   // "Qpac_1/TIDPs/Structural"
    public string? DriveFolderId { get; set; }
    public DataTarget Target { get; set; } = DataTarget.Live;
    public int SortOrder { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Project? Project { get; set; }
    public Folder? Parent { get; set; }
    public ICollection<Folder> Children { get; set; } = new List<Folder>();
    public ICollection<FolderFile> Files { get; set; } = new List<FolderFile>();
}

public class FolderFile : Entity
{
    public Guid FolderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DriveFileId { get; set; }
    public DateTime? DriveModifiedAt { get; set; }
    public string? Md5 { get; set; }
    public long SizeBytes { get; set; }
    public FileKind Kind { get; set; } = FileKind.Unknown;
    public FileSource Source { get; set; } = FileSource.Drive;
    public string? StoragePath { get; set; }                            // "App_Data/files/{id}.xlsx"
    public Guid? LastImportBatchId { get; set; }
    public ImportState State { get; set; } = ImportState.NotImported;

    public Folder? Folder { get; set; }
}
