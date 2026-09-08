using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.GetStatusMappings;

public sealed class GetStatusMappingsHandler
    : IQueryHandler<GetStatusMappingsQuery, IReadOnlyList<StatusMappingDto>>
{
    private readonly DipDbContext _db;

    public GetStatusMappingsHandler(DipDbContext db) => _db = db;

    public async Task<IReadOnlyList<StatusMappingDto>> Handle(
        GetStatusMappingsQuery query, CancellationToken ct) =>
        await _db.StatusMappings
            .AsNoTracking()
            .Where(m => m.ProjectId == query.ProjectId && (query.IncludeDeleted || !m.IsDeleted))
            .OrderBy(m => m.IsLegacy)
            .ThenBy(m => m.AconexStatus)
            .Select(m => new StatusMappingDto(
                m.Id, m.AconexStatus, m.Status, m.IsLegacy, m.IsDeleted, m.DeletedAt))
            .ToListAsync(ct);
}
