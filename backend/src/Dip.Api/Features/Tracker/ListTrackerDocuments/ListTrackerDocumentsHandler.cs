using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Tracker.ListTrackerDocuments;

public sealed class ListTrackerDocumentsHandler
    : IQueryHandler<ListTrackerDocumentsQuery, PagedResult<TrackerRowDto>>
{
    private readonly DipDbContext _db;

    public ListTrackerDocumentsHandler(DipDbContext db) => _db = db;

    public async Task<PagedResult<TrackerRowDto>> Handle(
        ListTrackerDocumentsQuery query, CancellationToken ct)
    {
        // Filtering and paging happen in SQL over the document/snapshot join, so a
        // page costs one page of rows rather than the whole project.
        var rows =
            from document in _db.Documents.AsNoTracking()
            join snapshotRow in _db.DocumentSnapshots.AsNoTracking()
                on document.Id equals snapshotRow.DocumentId into snapshots
            from snapshot in snapshots.DefaultIfEmpty()
            where document.ProjectId == query.ProjectId
            select new { document, snapshot };

        if (!string.IsNullOrWhiteSpace(query.Discipline))
        {
            rows = rows.Where(r => r.document.CorporateDiscipline == query.Discipline);
        }

        if (query.Status is not null)
        {
            rows = rows.Where(r => r.snapshot != null && r.snapshot.Status == query.Status);
        }

        if (query.HasAconex is not null)
        {
            rows = query.HasAconex.Value
                ? rows.Where(r => r.snapshot != null && r.snapshot.Revision != null)
                : rows.Where(r => r.snapshot == null || r.snapshot.Revision == null);
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{Escape(search)}%";
            rows = rows.Where(r =>
                EF.Functions.ILike(r.document.DocumentNumber, pattern, @"\")
                || EF.Functions.ILike(r.document.Title, pattern, @"\"));
        }

        var total = await rows.CountAsync(ct);

        var page = await rows
            .OrderBy(r => r.document.DocumentNumber)
            .ThenBy(r => r.document.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new { r.document.Id, r.snapshot })
            .ToListAsync(ct);

        // The exchanges (for the author column) are fetched only for the page's rows.
        var ids = page.Select(r => r.Id).ToList();
        var documents = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Exchanges)
            .Where(d => ids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);

        var items = page
            .Select(r => TrackerRowDto.From(documents[r.Id], ToRow(documents[r.Id], r.snapshot)))
            .ToList();

        return new PagedResult<TrackerRowDto>(items, query.Page, query.PageSize, total);
    }

    private static TrackerRow ToRow(Document document, DocumentSnapshot? snapshot) =>
        snapshot is null
            ? new TrackerRow(document.Id, document.DocumentNumber,
                null, null, null, null, null, null, null, null, null, null, null)
            : TrackerRow.FromSnapshot(snapshot, document.DocumentNumber);

    private static string Escape(string value) => value
        .Replace(@"\", @"\\", StringComparison.Ordinal)
        .Replace("%", @"\%", StringComparison.Ordinal)
        .Replace("_", @"\_", StringComparison.Ordinal);
}
