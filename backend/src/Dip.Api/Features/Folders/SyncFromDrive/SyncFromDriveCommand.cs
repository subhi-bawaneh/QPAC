using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.SyncFromDrive;

// Chunked: one call processes one Drive folder (its immediate children +
// downloads the .xlsx files below the size cap). Returns the next folder id
// to process; the client re-invokes until Done=true.
//
// This matches PLAN.md § 4 - shared hosting has no background workers, so a
// long single request would exceed IIS request timeout.
[Permission(Permissions.DriveSync)]
public sealed record SyncFromDriveCommand(
    Guid ProjectId,
    string? StartFolderDriveId,
    bool DownloadFiles = false) : ICommand<SyncStepResult>;

public sealed record SyncStepResult(
    int FoldersUpserted,
    int FilesUpserted,
    int FilesDownloaded,
    string? NextFolderDriveId,
    bool Done);
