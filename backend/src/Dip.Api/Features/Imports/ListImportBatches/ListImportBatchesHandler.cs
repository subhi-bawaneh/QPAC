using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Imports.ListImportBatches;

public sealed class ListImportBatchesHandler : IQueryHandler<ListImportBatchesQuery, IReadOnlyCollection<ImportBatchSummary>>
{
    private readonly DipDbContext _db;

    public ListImportBatchesHandler(DipDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<ImportBatchSummary>> Handle(ListImportBatchesQuery query, CancellationToken ct)
    {
        var q = _db.ImportBatches
            .AsNoTracking()
            .Where(b => b.ProjectId == query.ProjectId);

        if (query.Kind is not null) q = q.Where(b => b.Kind == query.Kind.Value);
        if (query.TidpFileId is not null) q = q.Where(b => b.TidpFileId == query.TidpFileId.Value);

        var take = Math.Clamp(query.Take, 1, 500);
        var rows = await q
            .OrderByDescending(b => b.ImportedAt)
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(ImportBatchSummary.From).ToList();
    }
}
