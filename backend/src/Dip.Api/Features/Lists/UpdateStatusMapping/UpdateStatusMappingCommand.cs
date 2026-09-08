using Dip.Api.Features.Lists.GetStatusMappings;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Lists.UpdateStatusMapping;

[Permission(Permissions.ListsManage)]
public sealed record UpdateStatusMappingCommand(
    Guid Id,
    string AconexStatus,
    UnifiedStatus Status,
    bool IsLegacy) : ICommand<StatusMappingDto>;
