using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Tracker.ListTrackerDocuments;

// The snapshot table carries every column the grid shows, so filtering, sorting and
// paging are one SQL query over one table — for both layers at once.
public sealed class ListTrackerDocumentsHandler
    : IQueryHandler<ListTrackerDocumentsQuery, PagedResult<TrackerRowDto>>
{
    private readonly DipDbContext _db;

    public ListTrackerDocumentsHandler(DipDbContext db) => _db = db;

    public async Task<PagedResult<TrackerRowDto>> Handle(
        ListTrackerDocumentsQuery query, CancellationToken ct)
    {
        var rows = _db.DocumentSnapshots.AsNoTracking()
            .Where(s => s.ProjectId == query.ProjectId);

        if (!string.IsNullOrWhiteSpace(query.Discipline))
        {
            rows = rows.Where(s => s.Discipline == query.Discipline);
        }

        if (query.Status is not null)
        {
            rows = rows.Where(s => s.Status == query.Status);
        }

        if (query.HasAconex is not null)
        {
            rows = query.HasAconex.Value
                ? rows.Where(s => s.Revision != null)
                : rows.Where(s => s.Revision == null);
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{Escape(search)}%";
            rows = rows.Where(s =>
                EF.Functions.ILike(s.DocumentNumber, pattern, @"\")
                || EF.Functions.ILike(s.Title, pattern, @"\"));
        }

        var total = await rows.CountAsync(ct);

        var page = await rows
            .OrderBy(s => s.DocumentNumber)
            .ThenBy(s => s.DocumentId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var items = page.Select(TrackerRowDto.From).ToList();
        return new PagedResult<TrackerRowDto>(items, query.Page, query.PageSize, total);
    }

    private static string Escape(string value) => value
        .Replace(@"\", @"\\", StringComparison.Ordinal)
        .Replace("%", @"\%", StringComparison.Ordinal)
        .Replace("_", @"\_", StringComparison.Ordinal);
}
