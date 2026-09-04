namespace Dip.Api.Features.Users;

public sealed record UserListItem(
    Guid Id,
    string Email,
    string FullName,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> DisciplineCodes);

public sealed record UserDetail(
    Guid Id,
    string Email,
    string FullName,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<string> DisciplineCodes,
    DateTime CreatedAt);
