using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Imports.RunImportStep;

public sealed class RunImportStepHandler : ICommandHandler<RunImportStepCommand, RunStepResult>
{
    private readonly DipDbContext _db;
    private readonly ImportDispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;

    public RunImportStepHandler(DipDbContext db, ImportDispatcher dispatcher, ICurrentUser currentUser)
    {
        _db = db;
        _dispatcher = dispatcher;
        _currentUser = currentUser;
    }

    public async Task<RunStepResult> Handle(RunImportStepCommand command, CancellationToken ct)
    {
        var batch = await _db.ImportBatches
            .Include(b => b.FolderFile)
            .FirstOrDefaultAsync(b => b.Id == command.ImportBatchId, ct)
            ?? throw new KeyNotFoundException($"ImportBatch {command.ImportBatchId} not found");

        // Idempotent: once Completed, subsequent calls just report status.
        if (batch.Completed)
        {
            return Summarise(batch, batch.RowsRead, done: true);
        }

        var file = batch.FolderFile
            ?? throw new InvalidOperationException("ImportBatch has no FolderFile");
        var storagePath = file.StoragePath
            ?? throw new InvalidOperationException("FolderFile has no local StoragePath");

        var result = await _dispatcher.RunAsync(batch, storagePath, _currentUser.UserName ?? "system", ct);
        return Summarise(batch, result.RowsRead, done: true);
    }

    private static RunStepResult Summarise(Domain.Entities.ImportBatch batch, int total, bool done) =>
        new(
            batch.Id,
            Processed: batch.RowsRead,
            Total: total,
            Done: done,
            Batch: new ImportBatchSummary(
                batch.Id, batch.ProjectId, batch.Kind, batch.Target,
                batch.FolderFileId, batch.FileName, batch.ImportedAt, batch.ImportedBy,
                batch.RowsRead, batch.RowsInserted, batch.RowsUpdated, batch.RowsSkipped,
                batch.Completed, batch.Log));
}
