using Dip.Application.Abstractions;
using Dip.Infrastructure.Identity;
using Dip.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Users.ListUsers;

public sealed class ListUsersHandler : IQueryHandler<ListUsersQuery, IReadOnlyCollection<UserListItem>>
{
    private readonly DipDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ISqlDialect _dialect;

    public ListUsersHandler(DipDbContext db, UserManager<ApplicationUser> users, ISqlDialect dialect)
    {
        _db = db;
        _users = users;
        _dialect = dialect;
    }

    public async Task<IReadOnlyCollection<UserListItem>> Handle(ListUsersQuery query, CancellationToken ct)
    {
        var q = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            // SQLite has no ILIKE, and its LIKE is case-insensitive for ASCII already.
            q = _dialect.SupportsILike
                ? q.Where(u =>
                    (u.Email != null && EF.Functions.ILike(u.Email, $"%{term}%")) ||
                    EF.Functions.ILike(u.FullName, $"%{term}%"))
                : q.Where(u =>
                    (u.Email != null && EF.Functions.Like(u.Email, $"%{term}%")) ||
                    EF.Functions.Like(u.FullName, $"%{term}%"));
        }

        var users = await q.OrderBy(u => u.Email).ToListAsync(ct);

        var result = new List<UserListItem>(users.Count);
        foreach (var user in users)
        {
            var roles = await _users.GetRolesAsync(user);
            var claims = await _users.GetClaimsAsync(user);
            var disciplines = claims.Where(c => c.Type == "discipline").Select(c => c.Value).ToArray();
            result.Add(new UserListItem(
                user.Id, user.Email ?? string.Empty, user.FullName, user.IsActive,
                roles.ToArray(), disciplines));
        }

        return result;
    }
}
