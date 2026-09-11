using Dip.Api.Features.Tidps.UploadTidpFile;
using Dip.Api.Hubs;
using Dip.Api.Workers;
using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.UploadPicklists;

public sealed class UploadPicklistsHandler : ICommandHandler<UploadPicklistsCommand, UploadAccepted>
{
    private readonly DipDbContext _db;
    private readonly WorkQueue _queue;
    private readonly ISyncNotifier _notifier;
    private readonly ICurrentUser _user;

    public UploadPicklistsHandler(
        DipDbContext db, WorkQueue queue, ISyncNotifier notifier, ICurrentUser user)
    {
        _db = db;
        _queue = queue;
        _notifier = notifier;
        _user = user;
    }

    public async Task<UploadAccepted> Handle(UploadPicklistsCommand command, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == command.ProjectId, ct)
            ?? throw new KeyNotFoundException($"Project {command.ProjectId} not found");

        var batch = new ImportBatch
        {
            ProjectId = project.Id,
            Kind = ImportKind.Picklists,
            FileName = command.FileName,
            ImportedAt = DateTime.UtcNow,
            UploadedBy = _user.UserName ?? "system",
            Status = ImportBatchStatus.Queued,
        };
        _db.ImportBatches.Add(batch);

        var projectId = project.Id;
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

        return new UploadAccepted(Guid.Empty, batch.Id, command.FileName);
    }
}
