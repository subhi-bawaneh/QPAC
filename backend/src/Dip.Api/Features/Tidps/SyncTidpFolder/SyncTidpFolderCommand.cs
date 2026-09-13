using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Tidps.SyncTidpFolder;

// The whole TIDP folder in one operation. Like every other source upload this is the
// super admin's alone, and like every other one the bytes are parsed and discarded.
[Permission(Permissions.FilesManage)]
public sealed record SyncTidpFolderCommand(
    Guid ProjectId,
    string? RootName,

    // Every folder the client walked, including the empty ones. A multipart upload
    // carries files, and an empty discipline folder has none — `01.NAP/ID-Interior
    // Design` would disappear from the register the moment it was emptied. A client
    // that cannot enumerate its folders (a browser's `webkitdirectory` cannot) leaves
    // this out and the folder set is taken from the file paths instead.
    IReadOnlyList<string> Folders,

    IReadOnlyList<UploadedTidpFile> Files) : ICommand<TidpFolderSyncResult>;

// One file as it arrived: its path relative to the folder the operator picked, the
// timestamp the client read off disk (LastWriteTimeUtc), and its bytes.
public sealed record UploadedTidpFile(
    string RelativePath,
    DateTime? LastModifiedUtc,
    long SizeBytes,
    byte[] Content);

// What the sync did, file by file. Returned by the POST as the plan — Added and
// Updated mean "queued for import" at that point — and by the GET as the outcome,
// with each file's real import status overlaid.
public sealed record TidpFolderSyncResult(
    Guid SyncId,
    Guid ProjectId,
    string RootName,
    DateTime StartedAt,
    int TotalFiles,
    int Added,
    int Updated,
    int Skipped,
    int Missing,
    int Failed,
    IReadOnlyList<TidpFolderSyncFile> Files);

public sealed record TidpFolderSyncFile(
    string RelativePath,
    TidpSyncAction Action,
    string? OwnerName,
    TidpOwnerType? OwnerType,
    int? OwnerSortOrder,
    string? DisciplineCode,
    string? DisciplineName,
    string? DisciplineTag,
    string? Sequence,
    string? Error,

    // Populated for Added and Updated: the row and the import batch the file went to,
    // so a caller can follow it on the hub without a second lookup.
    Guid? TidpFileId = null,
    Guid? BatchId = null,

    // A path that vanished whose name and hash turned up somewhere else, and the other
    // half of the same pair. A suggestion, never an action: nothing is moved or
    // deleted on the strength of it.
    string? MovedTo = null,
    string? MovedFrom = null,

    IReadOnlyList<string>? Warnings = null,

    // Filled in only by the GET, from the file's own row once its import has run.
    TidpFileStatus? ImportStatus = null,
    int? RowsRead = null,
    int? RowsImported = null,
    int? RowsSkipped = null);
