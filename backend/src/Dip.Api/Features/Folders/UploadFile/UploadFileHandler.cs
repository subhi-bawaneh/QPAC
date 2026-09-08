using Dip.Api.Hubs;
using Dip.Api.Workers;
using Dip.Application.Abstractions;
using Dip.Application.Files;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.UploadFile;

// An upload always wins over whatever Drive last wrote for the same name
// (refactor-plan § 3 R2): it replaces the blob, stamps the current time and
// re-queues the import.
public sealed class UploadFileHandler : ICommandHandler<UploadFileCommand, UploadResult>
{
    private readonly DipDbContext _db;
    private readonly WorkQueue _queue;
    private readonly ISyncNotifier _notifier;

    public UploadFileHandler(DipDbContext db, WorkQueue queue, ISyncNotifier notifier)
    {
        _db = db;
        _queue = queue;
        _notifier = notifier;
    }

    public async Task<UploadResult> Handle(UploadFileCommand command, CancellationToken ct)
    {
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == command.FolderId && !f.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Folder {command.FolderId} not found");

        var lowered = command.FileName.ToLowerInvariant();
        var file = await _db.FolderFiles
            .FirstOrDefaultAsync(f => f.FolderId == folder.Id && f.Name.ToLower() == lowered && !f.IsDeleted, ct);

        var replaced = file is not null;
        if (file is null)
        {
            file = new FolderFile { FolderId = folder.Id, Name = command.FileName };
            _db.FolderFiles.Add(file);
        }

        file.Name = command.FileName;
        file.Kind = FileKindDetector.Detect(command.FileName);
        file.ContentSource = FileSource.Upload;
        file.ContentModifiedAt = DateTime.UtcNow;
        file.ContentMd5 = DriveSyncService.Md5Of(command.Content);
        file.SizeBytes = command.Content.LongLength;
        file.State = ImportState.NotImported;
        file.ImportError = null;
        file.IsDeleted = false;

        var blob = await _db.FileBlobs.FirstOrDefaultAsync(b => b.FolderFileId == file.Id, ct);
        if (blob is null)
        {
            _db.FileBlobs.Add(new FileBlob { FolderFileId = file.Id, Content = command.Content });
        }
        else
        {
            blob.Content = command.Content;
        }

        // The import must not start before the row exists, so the queue is fed after
        // the transaction behavior commits.
        var fileId = file.Id;
        _db.SavedChanges += Enqueue;

        void Enqueue(object? sender, SavedChangesEventArgs args)
        {
            _db.SavedChanges -= Enqueue;
            _queue.EnqueueImport(fileId);
            _ = _notifier.FileQueuedAsync(folder.ProjectId, fileId, folder.Id);
        }

        return new UploadResult(file.Id, file.Name, replaced);
    }
}
