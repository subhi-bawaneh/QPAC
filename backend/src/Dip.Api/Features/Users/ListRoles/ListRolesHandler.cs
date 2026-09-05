using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Users.ListRoles;

public sealed class ListRolesHandler : IQueryHandler<ListRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly DipDbContext _db;

    public ListRolesHandler(DipDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoleDto>> Handle(ListRolesQuery query, CancellationToken ct)
    {
        _ = query;

        var roles = await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(ct);

        var claims = await _db.RoleClaims
            .AsNoTracking()
            .Where(c => c.ClaimType == "permission")
            .Select(c => new { c.RoleId, c.ClaimValue })
            .ToListAsync(ct);

        var byRole = claims
            .GroupBy(c => c.RoleId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(c => c.ClaimValue ?? string.Empty).OrderBy(v => v).ToList());

        return roles
            .Select(r => new RoleDto(
                r.Name ?? string.Empty,
                byRole.TryGetValue(r.Id, out var permissions) ? permissions : Array.Empty<string>()))
            .ToList();
    }
}
