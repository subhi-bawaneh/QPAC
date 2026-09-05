using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Users.ListRoles;

// The roles a user can be given, with the permissions each one carries. Read-only:
// roles are seeded from RoleDefinitions, so this is a reference list, not a CRUD.
[Permission(Permissions.UsersManage)]
public sealed record ListRolesQuery : IQuery<IReadOnlyList<RoleDto>>;

public sealed record RoleDto(string Name, IReadOnlyList<string> Permissions);
