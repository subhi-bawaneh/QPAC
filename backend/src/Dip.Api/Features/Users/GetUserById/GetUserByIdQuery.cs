using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Users.GetUserById;

[Permission(Permissions.UsersManage)]
public sealed record GetUserByIdQuery(Guid Id) : IQuery<UserDetail>;
