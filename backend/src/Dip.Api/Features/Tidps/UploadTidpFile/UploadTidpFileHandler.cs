using Dip.Api.Hubs;
using Dip.Api.Workers;
using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Tidps.UploadTidpFile;

// Creates the TidpFile row and queues the import. The discipline is read from the
// workbook's own header block before anything is written, so a file that is not a TIDP
// is refused here rather than leaving an empty row behind.
public sealed class UploadTidpFileHandler : ICommandHandler<UploadTidpFileCommand, UploadAccepted>
{
    private readonly DipDbContext _db;
    private readonly TidpImporter _importer;
    private readonly WorkQueue _queue;
    private readonly ISyncNotifier _notifier;
    private readonly ICurrentUser _user;

    public UploadTidpFileHandler(
        DipDbContext db, TidpImporter importer, WorkQueue queue,
        ISyncNotifier notifier, ICurrentUser user)
    {
        _db = db;
        _importer = importer;
        _queue = queue;
        _notifier = notifier;
        _user = user;
    }

    public async Task<UploadAccepted> Handle(UploadTidpFileCommand command, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == command.ProjectId, ct)
            ?? throw new KeyNotFoundException($"Project {command.ProjectId} not found");

        var disciplineName = _importer.ReadDiscipline(new MemoryStream(command.Content, writable: false));
        var discipline = await _db.Disciplines
            .FirstOrDefaultAsync(d => d.ProjectId == project.Id
                && (d.CorporateName == disciplineName || d.Code == disciplineName), ct);

        if (discipline is null)
        {
            discipline = new Discipline
            {
                ProjectId = project.Id,
                Code = disciplineName.Length > 20 ? disciplineName[..20] : disciplineName,
                CorporateName = disciplineName,
            };
            _db.Disciplines.Add(discipline);
        }

        var now = DateTime.UtcNow;
        var by = _user.UserName ?? "system";

        var file = new TidpFile
        {
            ProjectId = project.Id,
            DisciplineId = discipline.Id,
            FileName = command.FileName,
            UploadedBy = by,
            UploadedAt = now,
            Status = TidpFileStatus.Importing,
            CreatedAt = now,
            CreatedBy = by,
            UpdatedAt = now,
            UpdatedBy = by,
        };
        _db.TidpFiles.Add(file);

        var batch = new ImportBatch
        {
            ProjectId = project.Id,
            Kind = ImportKind.Tidp,
            TidpFileId = file.Id,
            FileName = command.FileName,
            ImportedAt = now,
            UploadedBy = by,
            Status = ImportBatchStatus.Queued,
        };
        _db.ImportBatches.Add(batch);

        // The import must not start before the rows exist, so the queue is fed after
        // the transaction behavior commits. The bytes ride in the work item: nothing is
        // stored, so the queue holds the only copy until the worker is done with it.
        QueueAfterSave(project.Id, batch.Id, command.FileName, command.Content);

        return new UploadAccepted(file.Id, batch.Id, command.FileName);
    }

    private void QueueAfterSave(Guid projectId, Guid batchId, string fileName, byte[] content)
    {
        _db.SavedChanges += Enqueue;

        void Enqueue(object? sender, Microsoft.EntityFrameworkCore.SavedChangesEventArgs args)
        {
            _db.SavedChanges -= Enqueue;
            _queue.EnqueueImport(batchId, content);
            _ = _notifier.ImportQueuedAsync(projectId, batchId, fileName);
        }
    }
}
