using Dip.Api.Hubs;
using Dip.Api.Workers;
using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Aconex.UploadAconex;

public sealed class UploadAconexHandler : ICommandHandler<UploadAconexCommand, AconexUploadAccepted>
{
    private readonly DipDbContext _db;
    private readonly WorkQueue _queue;
    private readonly ISyncNotifier _notifier;
    private readonly ICurrentUser _user;

    public UploadAconexHandler(
        DipDbContext db, WorkQueue queue, ISyncNotifier notifier, ICurrentUser user)
    {
        _db = db;
        _queue = queue;
        _notifier = notifier;
        _user = user;
    }

    public async Task<AconexUploadAccepted> Handle(UploadAconexCommand command, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == command.ProjectId, ct)
            ?? throw new KeyNotFoundException($"Project {command.ProjectId} not found");

        var by = _user.UserName ?? "system";
        var now = DateTime.UtcNow;
        var queued = new List<(Guid BatchId, string FileName, byte[] Content)>(command.Files.Count);

        foreach (var file in command.Files)
        {
            var batch = new ImportBatch
            {
                ProjectId = project.Id,
                Kind = ImportKind.AconexHistory,
                FileName = file.FileName,
                ImportedAt = now,
                UploadedBy = by,
                Status = ImportBatchStatus.Queued,
            };
            _db.ImportBatches.Add(batch);
            queued.Add((batch.Id, file.FileName, file.Content));
        }

        // The worker runs one item at a time, so several files queued together are
        // imported in order — which matters: the second file's duplicate count is only
        // right if the first has already landed.
        var projectId = project.Id;
        _db.SavedChanges += Enqueue;

        void Enqueue(object? sender, SavedChangesEventArgs args)
        {
            _db.SavedChanges -= Enqueue;
            foreach (var (batchId, fileName, content) in queued)
            {
                _queue.EnqueueImport(batchId, content);
                _ = _notifier.ImportQueuedAsync(projectId, batchId, fileName);
            }
        }

        return new AconexUploadAccepted(
            queued.Select(q => new AconexQueuedFile(q.BatchId, q.FileName)).ToList());
    }
}
