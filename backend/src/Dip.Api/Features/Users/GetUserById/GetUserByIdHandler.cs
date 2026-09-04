using Dip.Application.Abstractions;
using Dip.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Dip.Api.Features.Users.GetUserById;

public sealed class GetUserByIdHandler : IQueryHandler<GetUserByIdQuery, UserDetail>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;

    public GetUserByIdHandler(UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<UserDetail> Handle(GetUserByIdQuery query, CancellationToken ct)
    {
        _ = ct;
        var user = await _users.FindByIdAsync(query.Id.ToString())
            ?? throw new KeyNotFoundException($"User {query.Id} not found");

        var roles = await _users.GetRolesAsync(user);
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roleName in roles)
        {
            var role = await _roles.FindByNameAsync(roleName);
            if (role is null) continue;
            foreach (var claim in (await _roles.GetClaimsAsync(role)).Where(c => c.Type == "permission"))
            {
                permissions.Add(claim.Value);
            }
        }

        var claims = await _users.GetClaimsAsync(user);
        var disciplines = claims.Where(c => c.Type == "discipline").Select(c => c.Value).ToArray();

        return new UserDetail(
            user.Id, user.Email ?? string.Empty, user.FullName, user.IsActive,
            roles.ToArray(), permissions.ToArray(), disciplines, user.CreatedAt);
    }
}
