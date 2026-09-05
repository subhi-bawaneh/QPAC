using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.GetPicklists;

public sealed class GetPicklistsHandler
    : IQueryHandler<GetPicklistsQuery, IReadOnlyList<PicklistGroupDto>>
{
    private readonly DipDbContext _db;

    public GetPicklistsHandler(DipDbContext db) => _db = db;

    public async Task<IReadOnlyList<PicklistGroupDto>> Handle(
        GetPicklistsQuery query, CancellationToken ct)
    {
        var items = await _db.PicklistItems
            .AsNoTracking()
            .Where(p => p.ProjectId == query.ProjectId)
            .OrderBy(p => p.Field)
            .ThenBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
            .ToListAsync(ct);

        return items
            .GroupBy(p => p.Field)
            .Select(g => new PicklistGroupDto(
                g.Key,
                g.Select(p => new PicklistItemDto(p.Id, p.Code, p.Description, p.SortOrder)).ToList()))
            .ToList();
    }
}
