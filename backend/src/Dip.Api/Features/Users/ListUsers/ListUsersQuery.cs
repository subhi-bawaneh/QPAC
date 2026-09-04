using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Users.ListUsers;

[Permission(Permissions.UsersManage)]
public sealed record ListUsersQuery(string? Search = null) : IQuery<IReadOnlyCollection<UserListItem>>;
