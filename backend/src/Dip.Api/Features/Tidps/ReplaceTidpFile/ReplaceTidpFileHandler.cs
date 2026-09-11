using Dip.Api.Features.Audit;
using Dip.Api.Features.Tidps.UploadTidpFile;
using Dip.Api.Hubs;
using Dip.Api.Workers;
using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Tidps.ReplaceTidpFile;

public sealed class ReplaceTidpFileHandler : ICommandHandler<ReplaceTidpFileCommand, UploadAccepted>
{
    private readonly DipDbContext _db;
    private readonly WorkQueue _queue;
    private readonly ISyncNotifier _notifier;
    private readonly ICurrentUser _user;

    public ReplaceTidpFileHandler(
        DipDbContext db, WorkQueue queue, ISyncNotifier notifier, ICurrentUser user)
    {
        _db = db;
        _queue = queue;
        _notifier = notifier;
        _user = user;
    }

    public async Task<UploadAccepted> Handle(ReplaceTidpFileCommand command, CancellationToken ct)
    {
        var file = await _db.TidpFiles.FirstOrDefaultAsync(t => t.Id == command.TidpFileId, ct)
            ?? throw new KeyNotFoundException($"TIDP file {command.TidpFileId} not found");

        var now = DateTime.UtcNow;
        var by = _user.UserName ?? "system";

        // The dump comes first: once the rows are gone the only record of what they held
        // is this one. It runs inside the command's transaction, so a failure leaves
        // neither the audit rows nor the deletion.
        await AuditDump.DocumentsAsync(_db, file.ProjectId, file.Id, AuditDump.Replaced, by, ct);
        await _db.SaveChangesAsync(ct);

        // Only this file's rows. A number another TidpFile owns was never this file's to
        // delete — first-file-wins on the way in, and the same on the way out.
        await _db.Documents.Where(d => d.TidpFileId == file.Id).ExecuteDeleteAsync(ct);

        file.FileName = command.FileName;
        file.UploadedBy = by;
        file.UploadedAt = now;
        file.Status = TidpFileStatus.Importing;
        file.Error = null;
        file.UpdatedAt = now;
        file.UpdatedBy = by;

        var batch = new ImportBatch
        {
            ProjectId = file.ProjectId,
            Kind = ImportKind.Tidp,
            TidpFileId = file.Id,
            FileName = command.FileName,
            ImportedAt = now,
            UploadedBy = by,
            Status = ImportBatchStatus.Queued,
        };
        _db.ImportBatches.Add(batch);

        var projectId = file.ProjectId;
        var batchId = batch.Id;
        var content = command.Content;
        var fileName = command.FileName;

        _db.SavedChanges += Enqueue;

        void Enqueue(object? sender, SavedChangesEventArgs args)
        {
            _db.SavedChanges -= Enqueue;
            _queue.EnqueueImport(batchId, content);
            _ = _notifier.ImportQueuedAsync(projectId, batchId, fileName);
        }

        return new UploadAccepted(file.Id, batch.Id, command.FileName);
    }
}
