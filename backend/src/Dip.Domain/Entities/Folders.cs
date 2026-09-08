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

    // A company folder is one whose contents belong to a single delivery partner
    // (refactor-plan § 3 R6). Author points at the PicklistItem (Field = Author).
    public bool IsCompany { get; set; }
    public Guid? AuthorId { get; set; }

    public Project? Project { get; set; }
    public Folder? Parent { get; set; }
    public PicklistItem? Author { get; set; }
    public ICollection<Folder> Children { get; set; } = new List<Folder>();
    public ICollection<FolderFile> Files { get; set; } = new List<FolderFile>();
}

// One workbook inside a folder. Identity is (FolderId, lower(Name)) among the
// non-deleted rows: Drive sync and UI upload upsert the same row and the newest
// content wins (refactor-plan § 3 R1/R2). Bytes live in FileBlob, never on disk.
public class FolderFile : Entity
{
    public Guid FolderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public FileKind Kind { get; set; } = FileKind.Unknown;

    // Drive identity — set while the file is (or was) mirrored from Drive.
    public string? DriveFileId { get; set; }
    public DateTime? DriveModifiedAt { get; set; }

    // Whoever wrote the bytes currently in FileBlob, and when.
    public FileSource ContentSource { get; set; } = FileSource.Drive;
    public DateTime ContentModifiedAt { get; set; }
    public string ContentMd5 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public ImportState State { get; set; } = ImportState.NotImported;
    public string? ImportError { get; set; }
    public Guid? LastImportBatchId { get; set; }
    public DateTime? LastImportedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Folder? Folder { get; set; }
    public FileBlob? Blob { get; set; }
}

// The workbook bytes. Separate table so listing files never drags megabytes
// of bytea along, and so "no file storage on the server" (decision D2) holds.
public class FileBlob
{
    public Guid FolderFileId { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public FolderFile? FolderFile { get; set; }
}
