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
    FileSource Source,
    ImportState State,
    long SizeBytes,
    DateTime? DriveModifiedAt,
    string? DriveFileId,
    string? Md5);

public sealed record FolderDetail(
    FolderNode Folder,
    IReadOnlyList<FolderNode> Subfolders,
    IReadOnlyList<FolderFileDto> Files);
