using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Lists.DeleteStatusMapping;

[Permission(Permissions.ListsManage)]
public sealed record DeleteStatusMappingCommand(Guid Id) : ICommand<Unit>;
