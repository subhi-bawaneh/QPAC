using Dip.Domain.Enums;

namespace Dip.Api.Features.Folders;

public sealed record FolderNode(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Path,
    DataTarget Target,
    string? DriveFolderId,
    DateTime? LastSyncedAt,
    int ChildCount,
    int FileCount);

public sealed record FolderTreeNode(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Path,
    DataTarget Target,
    IReadOnlyList<FolderTreeNode> Children);

public sealed record FolderFileDto(
    Guid Id,
    string Name,
    FileKind Kind,
    FileSource ContentSource,
    DateTime ContentModifiedAt,
    string? DriveFileId,
    DateTime? DriveModifiedAt,
    long SizeBytes,
    ImportState State,
    string? ImportError,
    DateTime? LastImportedAt);

// 202 body of POST /api/folders/{id}/files — the import itself runs in the worker.
public sealed record UploadResult(Guid FileId, string Name, bool Replaced);

public sealed record DriveStatusDto(
    bool IsRunning,
    DateTime? LastRunStartedAt,
    DateTime? LastRunFinishedAt,
    string? LastRunError,
    DateTime? NextRunAt,
    int QueuedImports);

public sealed record TriggerSyncResult(bool Queued, bool AlreadyRunning);

public sealed record FolderDetail(
    FolderNode Folder,
    IReadOnlyList<FolderNode> Subfolders,
    IReadOnlyList<FolderFileDto> Files);
