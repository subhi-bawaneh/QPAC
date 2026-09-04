namespace Dip.Application.Abstractions;

public sealed record TokenPair(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt);

public sealed record UserPrincipal(
    Guid UserId,
    string Email,
    string FullName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<string> DisciplineCodes);

public interface IJwtTokenService
{
    // Mints a short-lived access token embedding all permissions the user's roles
    // grant plus any discipline claims. Refresh token is a large opaque string.
    TokenPair Issue(UserPrincipal user, string requestIp);
}
