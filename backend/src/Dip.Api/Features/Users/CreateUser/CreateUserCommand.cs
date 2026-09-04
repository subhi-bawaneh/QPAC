using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Users.CreateUser;

[Permission(Permissions.UsersManage)]
public sealed record CreateUserCommand(
    string Email,
    string Password,
    string FullName,
    IReadOnlyCollection<string> Roles) : ICommand<Guid>;
