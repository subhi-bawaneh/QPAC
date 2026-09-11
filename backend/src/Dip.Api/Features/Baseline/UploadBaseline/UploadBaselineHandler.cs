using Dip.Api.Features.Audit;
using Dip.Api.Features.Tidps.UploadTidpFile;
using Dip.Api.Hubs;
using Dip.Api.Workers;
using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Baseline.UploadBaseline;

public sealed class UploadBaselineHandler : ICommandHandler<UploadBaselineCommand, UploadAccepted>
{
    private readonly DipDbContext _db;
    private readonly WorkQueue _queue;
    private readonly ISyncNotifier _notifier;
    private readonly ICurrentUser _user;

    public UploadBaselineHandler(
        DipDbContext db, WorkQueue queue, ISyncNotifier notifier, ICurrentUser user)
    {
        _db = db;
        _queue = queue;
        _notifier = notifier;
        _user = user;
    }

    public async Task<UploadAccepted> Handle(UploadBaselineCommand command, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == command.ProjectId, ct)
            ?? throw new KeyNotFoundException($"Project {command.ProjectId} not found");

        var by = _user.UserName ?? "system";
        var now = DateTime.UtcNow;

        // BaselineImporter replaces, so the outgoing activities are recorded before the
        // importer runs rather than after it has already removed them.
        await AuditDump.BaselineAsync(_db, project.Id, AuditDump.Replaced, by, ct);

        var batch = new ImportBatch
        {
            ProjectId = project.Id,
            Kind = ImportKind.Baseline,
            FileName = command.FileName,
            ImportedAt = now,
            UploadedBy = by,
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
