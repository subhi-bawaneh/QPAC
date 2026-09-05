using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Imports.GetImportStatus;

public sealed class GetImportStatusHandler : IQueryHandler<GetImportStatusQuery, ImportBatchSummary>
{
    private readonly DipDbContext _db;

    public GetImportStatusHandler(DipDbContext db) => _db = db;

    public async Task<ImportBatchSummary> Handle(GetImportStatusQuery query, CancellationToken ct)
    {
        var batch = await _db.ImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == query.ImportBatchId, ct)
            ?? throw new KeyNotFoundException($"ImportBatch {query.ImportBatchId} not found");

        return new ImportBatchSummary(
            batch.Id, batch.ProjectId, batch.Kind, batch.Target,
            batch.FolderFileId, batch.FileName, batch.ImportedAt, batch.ImportedBy,
            batch.RowsRead, batch.RowsInserted, batch.RowsUpdated, batch.RowsSkipped,
            batch.Completed, batch.Log);
    }
}
