using Dip.Api.Features.Lists.GetStatusMappings;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Lists.RestoreStatusMapping;

[Permission(Permissions.ListsManage)]
public sealed record RestoreStatusMappingCommand(Guid Id) : ICommand<StatusMappingDto>;
