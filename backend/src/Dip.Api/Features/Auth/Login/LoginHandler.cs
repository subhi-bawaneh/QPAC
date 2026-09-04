using System.Security.Claims;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Identity;
using Dip.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Auth.Login;

public sealed class LoginHandler : ICommandHandler<LoginCommand, AuthResult>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly IJwtTokenService _tokens;
    private readonly DipDbContext _db;

    public LoginHandler(
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        IJwtTokenService tokens,
        DipDbContext db)
    {
        _users = users;
        _roles = roles;
        _tokens = tokens;
        _db = db;
    }

    public async Task<AuthResult> Handle(LoginCommand command, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(command.Email);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("Invalid credentials");
        }

        var valid = await _users.CheckPasswordAsync(user, command.Password);
        if (!valid)
        {
            throw new UnauthorizedException("Invalid credentials");
        }

        var roleNames = await _users.GetRolesAsync(user);
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roleName in roleNames)
        {
            var role = await _roles.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }
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
            user.Email ?? command.Email,
            user.FullName,
            roleNames.ToArray(),
            permissions.ToArray(),
            disciplines);

        var pair = _tokens.Issue(principal, command.RequestIp);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = pair.RefreshToken,
            ExpiresAt = pair.RefreshTokenExpiresAt,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = command.RequestIp,
        });
        // TransactionBehavior calls SaveChangesAsync after we return.

        return new AuthResult(
            pair.AccessToken,
            pair.AccessTokenExpiresAt,
            pair.RefreshToken,
            pair.RefreshTokenExpiresAt,
            new UserSummary(user.Id, principal.Email, user.FullName, principal.Roles, principal.Permissions, disciplines));
    }
}
