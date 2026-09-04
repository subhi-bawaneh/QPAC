using System.Security.Claims;
using Dip.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Dip.Api.Common;

// Reads identity from the current HttpContext. Permission claims are looked up
// under the "permission" claim type; discipline claims under "discipline".
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserName => User?.Identity?.Name;

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public IReadOnlyCollection<string> DisciplineCodes =>
        User?.FindAll("discipline").Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public bool Has(string permission) => Permissions.Contains(permission, StringComparer.Ordinal);
}
