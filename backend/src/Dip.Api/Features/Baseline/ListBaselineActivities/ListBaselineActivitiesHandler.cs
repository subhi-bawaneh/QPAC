using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Baseline.ListBaselineActivities;

public sealed class ListBaselineActivitiesHandler
    : IQueryHandler<ListBaselineActivitiesQuery, PagedResult<BaselineActivityDto>>
{
    private readonly DipDbContext _db;

    public ListBaselineActivitiesHandler(DipDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<BaselineActivityDto>> Handle(
        ListBaselineActivitiesQuery query, CancellationToken ct)
    {
        // One grouped pass over the documents rather than a count per activity row.
        var documentCounts = await _db.Documents
            .AsNoTracking()
            .Where(d => d.ProjectId == query.ProjectId && d.ActivityId != null && d.ActivityId != "")
            .GroupBy(d => d.ActivityId!)
            .Select(g => new { ActivityCode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ActivityCode, x => x.Count, StringComparer.OrdinalIgnoreCase, ct);

        var activities = _db.BaselineActivities
            .AsNoTracking()
            .Where(b => b.ProjectId == query.ProjectId);

        if (query.Type is not null)
        {
            activities = activities.Where(b => b.Type == query.Type.Value);
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{Escape(search)}%";
            // Both SQL Server and SQLite are case-insensitive for LIKE already.
            activities = activities.Where(b =>
                EF.Functions.Like(b.ActivityCode, pattern, @"\")
                || EF.Functions.Like(b.Package, pattern, @"\"));
        }

        var rows = await activities
            .OrderBy(b => b.ActivityCode)
            .ToListAsync(ct);

        // Used is computed from the document counts, so it is filtered in memory.
        var mapped = rows
            .Select(b =>
            {
                var count = documentCounts.TryGetValue(b.ActivityCode, out var value) ? value : 0;
                return new BaselineActivityDto(
                    b.Id, b.ActivityCode, b.Package, b.Type, b.OriginalDuration,
                    b.Start, b.Finish, count, count > 0);
            })
            .Where(dto => query.Used is null || dto.Used == query.Used.Value)
            .ToList();

        var page = mapped
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new PagedResult<BaselineActivityDto>(page, query.Page, query.PageSize, mapped.Count);
    }

    private static string Escape(string value) => value
        .Replace(@"\", @"\\", StringComparison.Ordinal)
        .Replace("%", @"\%", StringComparison.Ordinal)
        .Replace("_", @"\_", StringComparison.Ordinal);
}
