using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Identity;
using Dip.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Auth.Refresh;

public sealed class RefreshHandler : ICommandHandler<RefreshCommand, AuthResult>
{
    private readonly DipDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly IJwtTokenService _tokens;

    public RefreshHandler(
        DipDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        IJwtTokenService tokens)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _tokens = tokens;
    }

    public async Task<AuthResult> Handle(RefreshCommand command, CancellationToken ct)
    {
        var existing = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == command.RefreshToken, ct);

        if (existing is null || existing.RevokedAt is not null || existing.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedException("Invalid or expired refresh token");
        }

        var user = existing.User ?? throw new UnauthorizedException("Refresh token has no user");
        if (!user.IsActive)
        {
            throw new UnauthorizedException("User is disabled");
        }

        var roleNames = await _users.GetRolesAsync(user);
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roleName in roleNames)
        {
            var role = await _roles.FindByNameAsync(roleName);
            if (role is null) continue;
            var roleClaims = await _roles.GetClaimsAsync(role);
            foreach (var claim in roleClaims.Where(c => c.Type == "permission"))
            {
                permissions.Add(claim.Value);
            }
        }

        var userClaims = await _users.GetClaimsAsync(user);
        var disciplines = userClaims
            .Where(c => c.Type == "discipline")
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var principal = new UserPrincipal(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            roleNames.ToArray(),
            permissions.ToArray(),
            disciplines);

        var pair = _tokens.Issue(principal, command.RequestIp);

        // Rotate: revoke the old token and store the new one.
        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedByIp = command.RequestIp;
        existing.ReplacedByToken = pair.RefreshToken;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = pair.RefreshToken,
            ExpiresAt = pair.RefreshTokenExpiresAt,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = command.RequestIp,
        });

        return new AuthResult(
            pair.AccessToken,
            pair.AccessTokenExpiresAt,
            pair.RefreshToken,
            pair.RefreshTokenExpiresAt,
            new UserSummary(user.Id, principal.Email, user.FullName, principal.Roles, principal.Permissions, disciplines));
    }
}
