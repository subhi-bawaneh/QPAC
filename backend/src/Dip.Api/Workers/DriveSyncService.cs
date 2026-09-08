using System.Security.Cryptography;
using Dip.Api.Hubs;
using Dip.Application.Abstractions;
using Dip.Application.Files;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Drive;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dip.Api.Workers;

public sealed record DriveSyncResult(int FoldersSynced, int FilesQueued, string? Error);

// Walks the Drive tree breadth-first from GoogleDrive:RootFolderId and mirrors it
// into Folders/FolderFiles/FileBlobs. Drive is read-only: nothing is ever written
// back to Google (decision D1), and nothing is written to the file system (D2).
//
// Content rules are refactor-plan § 3 R2: a Drive entry replaces the stored bytes
// only when it is both newer than what we hold and a different md5, so a UI upload
// made after the last Drive edit is never clobbered.
public sealed class DriveSyncService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xls", ".xlsm",
    };

    private readonly DipDbContext _db;
    private readonly IDriveClient _drive;
    private readonly WorkQueue _queue;
    private readonly ISyncNotifier _notifier;
    private readonly GoogleDriveOptions _options;
    private readonly ILogger<DriveSyncService> _logger;

    public DriveSyncService(
        DipDbContext db,
        IDriveClient drive,
        WorkQueue queue,
        ISyncNotifier notifier,
        IOptions<GoogleDriveOptions> options,
        ILogger<DriveSyncService> logger)
    {
        _db = db;
        _drive = drive;
        _queue = queue;
        _notifier = notifier;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DriveSyncResult> SyncProjectAsync(Guid projectId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.RootFolderId))
        {
            throw new InvalidOperationException("GoogleDrive:RootFolderId is not configured");
        }

        await _notifier.SyncStartedAsync(projectId, DateTime.UtcNow);

        var root = await UpsertRootAsync(projectId, ct);
        var pending = new Queue<Folder>();
        pending.Enqueue(root);

        var foldersSynced = 0;
        var filesQueued = 0;
        var errors = new List<string>();

        while (pending.Count > 0 && !ct.IsCancellationRequested)
        {
            var folder = pending.Dequeue();
            try
            {
                var queued = await SyncFolderAsync(projectId, folder, pending, ct);
                filesQueued += queued;
                foldersSynced++;
                await _notifier.FolderSyncedAsync(projectId, folder.Id, folder.Path, queued);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One unreadable folder must not abort the rest of the tree.
                _logger.LogError(ex, "Drive sync failed for folder {Path}", folder.Path);
                errors.Add($"{folder.Path}: {ex.Message}");
            }
        }

        var error = errors.Count == 0 ? null : string.Join("; ", errors);
        await _notifier.SyncFinishedAsync(projectId, foldersSynced, filesQueued, error);
        return new DriveSyncResult(foldersSynced, filesQueued, error);
    }

    private async Task<int> SyncFolderAsync(
        Guid projectId, Folder folder, Queue<Folder> pending, CancellationToken ct)
    {
        var children = await _drive.ListChildrenAsync(folder.DriveFolderId!, ct);

        var seenFolderDriveIds = new HashSet<string>(StringComparer.Ordinal);
        var seenFileIds = new HashSet<Guid>();
        var queued = 0;

        foreach (var entry in children)
        {
            ct.ThrowIfCancellationRequested();

            if (entry.IsFolder)
            {
                seenFolderDriveIds.Add(entry.Id);
                pending.Enqueue(await UpsertChildFolderAsync(projectId, folder, entry, ct));
            }
            else if (SupportedExtensions.Contains(Path.GetExtension(entry.Name)))
            {
                var (file, enqueued) = await UpsertFileAsync(projectId, folder, entry, ct);
                seenFileIds.Add(file.Id);
                if (enqueued) queued++;
            }
        }

        await SoftDeleteMissingAsync(folder, seenFolderDriveIds, seenFileIds, ct);

        folder.LastSyncedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return queued;
    }

    private async Task<Folder> UpsertRootAsync(Guid projectId, CancellationToken ct)
    {
        var folder = await _db.Folders
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.DriveFolderId == _options.RootFolderId, ct);
        if (folder is not null)
        {
            folder.IsDeleted = false;
            return folder;
        }

        folder = new Folder
        {
            ProjectId = projectId,
            ParentId = null,
            Name = "Drive Root",
            Path = "DriveRoot",
            DriveFolderId = _options.RootFolderId,
            Target = DataTarget.Live,
        };
        _db.Folders.Add(folder);
        await _db.SaveChangesAsync(ct);
        return folder;
    }

    private async Task<Folder> UpsertChildFolderAsync(
        Guid projectId, Folder parent, DriveEntry entry, CancellationToken ct)
    {
        var path = string.IsNullOrEmpty(parent.Path) ? entry.Name : $"{parent.Path}/{entry.Name}";
        var existing = await _db.Folders
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.DriveFolderId == entry.Id, ct);

        if (existing is null)
        {
            existing = new Folder
            {
                ProjectId = projectId,
                ParentId = parent.Id,
                Name = entry.Name,
                Path = path,
                DriveFolderId = entry.Id,
                // Target is inherited on insert only; an operator's later choice sticks.
                Target = parent.Target,
            };
            _db.Folders.Add(existing);
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        existing.Name = entry.Name;
        existing.Path = path;
        existing.ParentId = parent.Id;
        existing.IsDeleted = false;
        return existing;
    }

    // refactor-plan § 5.3: match on the folder+name pair first so a file re-uploaded
    // in Drive lands on the same row, then on the Drive id so a rename in Drive does too.
    private async Task<(FolderFile File, bool Enqueued)> UpsertFileAsync(
        Guid projectId, Folder folder, DriveEntry entry, CancellationToken ct)
    {
        var lowered = entry.Name.ToLowerInvariant();
        var file = await _db.FolderFiles
            .FirstOrDefaultAsync(f => f.FolderId == folder.Id && f.Name.ToLower() == lowered && !f.IsDeleted, ct)
            ?? await _db.FolderFiles
                .FirstOrDefaultAsync(f => f.DriveFileId == entry.Id && !f.IsDeleted, ct);

        var modified = entry.ModifiedTime ?? DateTime.UtcNow;

        if (file is null)
        {
            file = new FolderFile
            {
                FolderId = folder.Id,
                Name = entry.Name,
                Kind = FileKindDetector.Detect(entry.Name),
                ContentSource = FileSource.Drive,
                ContentModifiedAt = modified,
                ContentMd5 = string.Empty,
                State = ImportState.NotImported,
            };
            _db.FolderFiles.Add(file);
        }

        file.FolderId = folder.Id;
        file.DriveFileId = entry.Id;
        file.DriveModifiedAt = entry.ModifiedTime;
        file.Name = entry.Name;
        file.Kind = FileKindDetector.Detect(entry.Name);

        var shouldDownload = string.IsNullOrEmpty(file.ContentMd5)
            || (modified > file.ContentModifiedAt
                && !string.Equals(entry.Md5Checksum, file.ContentMd5, StringComparison.OrdinalIgnoreCase));

        if (!shouldDownload)
        {
            await _db.SaveChangesAsync(ct);
            return (file, false);
        }

        var bytes = await DownloadAsync(entry.Id, ct);

        file.ContentSource = FileSource.Drive;
        file.ContentModifiedAt = modified;
        file.ContentMd5 = Md5Of(bytes);
        file.SizeBytes = bytes.LongLength;
        file.State = ImportState.NotImported;
        file.ImportError = null;

        await _db.SaveChangesAsync(ct);
        await UpsertBlobAsync(file.Id, bytes, ct);

        _queue.EnqueueImport(file.Id);
        await _notifier.FileQueuedAsync(projectId, file.Id, folder.Id);
        return (file, true);
    }

    private async Task UpsertBlobAsync(Guid folderFileId, byte[] bytes, CancellationToken ct)
    {
        var blob = await _db.FileBlobs.FirstOrDefaultAsync(b => b.FolderFileId == folderFileId, ct);
        if (blob is null)
        {
            _db.FileBlobs.Add(new FileBlob { FolderFileId = folderFileId, Content = bytes });
        }
        else
        {
            blob.Content = bytes;
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<byte[]> DownloadAsync(string driveFileId, CancellationToken ct)
    {
        await using var stream = await _drive.DownloadAsync(driveFileId, ct);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    // Anything Drive-sourced that Drive no longer lists is soft-deleted. Uploaded
    // content is never removed by the poller, and a folder that still holds an
    // upload below it stays visible so that upload remains reachable.
    private async Task SoftDeleteMissingAsync(
        Folder folder, HashSet<string> seenFolderDriveIds, HashSet<Guid> seenFileIds, CancellationToken ct)
    {
        var staleFiles = await _db.FolderFiles
            .Where(f => f.FolderId == folder.Id
                && !f.IsDeleted
                && f.ContentSource == FileSource.Drive
                && f.DriveFileId != null
                && !seenFileIds.Contains(f.Id))
            .ToListAsync(ct);
        foreach (var file in staleFiles)
        {
            file.IsDeleted = true;
        }

        var staleFolders = await _db.Folders
            .Where(f => f.ParentId == folder.Id
                && !f.IsDeleted
                && f.DriveFolderId != null
                && !seenFolderDriveIds.Contains(f.DriveFolderId))
            .ToListAsync(ct);

        foreach (var stale in staleFolders)
        {
            if (await HasUploadedContentBelowAsync(stale.Id, ct)) continue;
            stale.IsDeleted = true;
        }
    }

    private async Task<bool> HasUploadedContentBelowAsync(Guid folderId, CancellationToken ct)
    {
        var frontier = new List<Guid> { folderId };
        while (frontier.Count > 0)
        {
            var hasUpload = await _db.FolderFiles.AnyAsync(
                f => frontier.Contains(f.FolderId) && !f.IsDeleted && f.ContentSource == FileSource.Upload, ct);
            if (hasUpload) return true;

            frontier = await _db.Folders
                .Where(f => f.ParentId != null && frontier.Contains(f.ParentId.Value) && !f.IsDeleted)
                .Select(f => f.Id)
                .ToListAsync(ct);
        }
        return false;
    }

    public static string Md5Of(byte[] bytes) =>
        Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
}
