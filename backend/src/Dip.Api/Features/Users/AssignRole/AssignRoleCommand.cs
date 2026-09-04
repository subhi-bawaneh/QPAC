using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Users.AssignRole;

[Permission(Permissions.UsersManage)]
public sealed record AssignRoleCommand(Guid UserId, IReadOnlyCollection<string> Roles) : ICommand<Unit>;
