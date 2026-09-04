using Microsoft.AspNetCore.Identity;

namespace Dip.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public class ApplicationRole : IdentityRole<Guid>
{
    // Custom permissions assigned to this role are stored as role claims of type "permission"
    // so they flow into the JWT via UserClaimsPrincipalFactory + role-claim expansion.
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByIp { get; set; } = string.Empty;
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
}
