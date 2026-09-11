using Dip.Domain.Entities;
using Dip.Application.Abstractions;
using Dip.Domain.Enums;
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
            .Where(p => p.ProjectId == query.ProjectId && (query.IncludeDeleted || !p.IsDeleted))
            .OrderBy(p => p.Field)
            .ThenBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
            .ToListAsync(ct);

        var byField = items.ToLookup(p => p.Field);

        // Every field appears, empty or not: the page renders one tab per list.
        return Enum.GetValues<PicklistField>()
            .Select(field => new PicklistGroupDto(
                field,
                byField[field].Select(PicklistItemDto.From).ToList()))
            .ToList();
    }
}
