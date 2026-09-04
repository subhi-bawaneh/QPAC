using Dip.Application.Abstractions;
using Dip.Application.Files;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Drive;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dip.Api.Features.Folders.SyncFromDrive;

// One call = one Drive folder. Walks BFS by:
//   1. Load the DIP-side folder linked to StartFolderDriveId (or the project root
//      when StartFolderDriveId is null).
//   2. Ask Google Drive for its immediate children.
//   3. Upsert each child folder as a DIP Folder (matched by DriveFolderId).
//   4. Upsert each child .xlsx as a FolderFile (matched by DriveFileId).
//   5. Compute NextFolderDriveId = the next unsynced child folder (across the
//      whole project), so the client can call again and again until Done.
public sealed class SyncFromDriveHandler : ICommandHandler<SyncFromDriveCommand, SyncStepResult>
{
    private readonly DipDbContext _db;
    private readonly IDriveClient _drive;
    private readonly ILocalFileStorage _storage;
    private readonly GoogleDriveOptions _driveOptions;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xls", ".xlsm",
    };

    public SyncFromDriveHandler(
        DipDbContext db,
        IDriveClient drive,
        ILocalFileStorage storage,
        IOptions<GoogleDriveOptions> driveOptions)
    {
        _db = db;
        _drive = drive;
        _storage = storage;
        _driveOptions = driveOptions.Value;
    }

    public async Task<SyncStepResult> Handle(SyncFromDriveCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_driveOptions.RootFolderId))
        {
            throw new InvalidOperationException("GoogleDrive:RootFolderId is not configured");
        }

        var startDriveId = command.StartFolderDriveId ?? _driveOptions.RootFolderId;

        // Ensure the DIP folder for the starting Drive folder exists.
        var parent = await UpsertRootIfNeededAsync(command.ProjectId, startDriveId, ct);

        var children = await _drive.ListChildrenAsync(startDriveId, ct);
        var foldersUpserted = 0;
        var filesUpserted = 0;
        var filesDownloaded = 0;

        foreach (var entry in children)
        {
            if (entry.IsFolder)
            {
                await UpsertChildFolderAsync(command.ProjectId, parent, entry, ct);
                foldersUpserted++;
            }
            else if (SupportedExtensions.Contains(Path.GetExtension(entry.Name)))
            {
                var (created, downloaded) = await UpsertChildFileAsync(parent, entry, command.DownloadFiles, ct);
                filesUpserted += created ? 1 : 0;
                filesDownloaded += downloaded ? 1 : 0;
            }
        }

        parent.LastSyncedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var next = await NextFolderDriveIdAsync(command.ProjectId, startDriveId, ct);
        return new SyncStepResult(foldersUpserted, filesUpserted, filesDownloaded, next, next is null);
    }

    private async Task<Folder> UpsertRootIfNeededAsync(Guid projectId, string driveId, CancellationToken ct)
    {
        var folder = await _db.Folders
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.DriveFolderId == driveId, ct);
        if (folder is not null)
        {
            return folder;
        }

        // For the project root we create a synthetic top-level entry named after the Drive folder id.
        folder = new Folder
        {
            ProjectId = projectId,
            ParentId = null,
            Name = "Drive Root",
            Path = "DriveRoot",
            DriveFolderId = driveId,
            Target = DataTarget.Live,
        };
        _db.Folders.Add(folder);
        await _db.SaveChangesAsync(ct);
        return folder;
    }

    private async Task UpsertChildFolderAsync(Guid projectId, Folder parent, DriveEntry entry, CancellationToken ct)
    {
        var existing = await _db.Folders
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.DriveFolderId == entry.Id, ct);

        var path = string.IsNullOrEmpty(parent.Path) ? entry.Name : $"{parent.Path}/{entry.Name}";

        if (existing is null)
        {
            _db.Folders.Add(new Folder
            {
                ProjectId = projectId,
                ParentId = parent.Id,
                Name = entry.Name,
                Path = path,
                DriveFolderId = entry.Id,
                Target = parent.Target,
            });
        }
        else
        {
            existing.Name = entry.Name;
            existing.Path = path;
            existing.ParentId = parent.Id;
            existing.IsDeleted = false;
        }
    }

    private async Task<(bool Created, bool Downloaded)> UpsertChildFileAsync(
        Folder parent, DriveEntry entry, bool downloadFiles, CancellationToken ct)
    {
        var existing = await _db.FolderFiles
            .FirstOrDefaultAsync(f => f.DriveFileId == entry.Id, ct);

        var created = false;
        if (existing is null)
        {
            existing = new FolderFile
            {
                FolderId = parent.Id,
                Name = entry.Name,
                DriveFileId = entry.Id,
                DriveModifiedAt = entry.ModifiedTime,
                Md5 = entry.Md5Checksum,
                SizeBytes = entry.Size ?? 0,
                Kind = FileKindDetector.Detect(entry.Name),
                Source = FileSource.Drive,
                State = ImportState.NotImported,
            };
            _db.FolderFiles.Add(existing);
            created = true;
        }
        else
        {
            existing.FolderId = parent.Id;
            existing.Name = entry.Name;
            existing.DriveModifiedAt = entry.ModifiedTime;
            existing.SizeBytes = entry.Size ?? existing.SizeBytes;
            // If Drive's md5 changed, mark the file as outdated so importers pick it up again.
            if (!string.IsNullOrEmpty(entry.Md5Checksum) && existing.Md5 != entry.Md5Checksum)
            {
                existing.Md5 = entry.Md5Checksum;
                if (existing.State == ImportState.Imported)
                {
                    existing.State = ImportState.Outdated;
                }
            }
        }

        var downloaded = false;
        if (downloadFiles && (created || existing.State != ImportState.Imported))
        {
            await using var stream = await _drive.DownloadAsync(entry.Id, ct);
            var stored = await _storage.SaveAsync(existing.Id, entry.Name, stream, ct);
            existing.StoragePath = stored.StoragePath;
            existing.SizeBytes = stored.SizeBytes;
            existing.Md5 = stored.Md5;
            downloaded = true;
        }

        return (created, downloaded);
    }

    // Next unsynced Drive folder in this project: LastSyncedAt is null AND has a DriveFolderId.
    // Deterministic order by Path so the client can display progress.
    private async Task<string?> NextFolderDriveIdAsync(Guid projectId, string currentDriveId, CancellationToken ct)
    {
        return await _db.Folders
            .Where(f => f.ProjectId == projectId
                && !f.IsDeleted
                && f.DriveFolderId != null
                && f.DriveFolderId != currentDriveId
                && f.LastSyncedAt == null)
            .OrderBy(f => f.Path)
            .Select(f => f.DriveFolderId)
            .FirstOrDefaultAsync(ct);
    }
}
