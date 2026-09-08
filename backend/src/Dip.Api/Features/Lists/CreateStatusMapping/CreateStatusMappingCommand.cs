using Dip.Api.Features.Lists.GetStatusMappings;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Lists.CreateStatusMapping;

// Like the picklists, creating a status that exists soft-deleted restores it (R9).
[Permission(Permissions.ListsManage)]
public sealed record CreateStatusMappingCommand(
    Guid ProjectId,
    string AconexStatus,
    UnifiedStatus Status,
    bool IsLegacy) : ICommand<CreateStatusMappingResult>;

public sealed record CreateStatusMappingResult(StatusMappingDto Item, bool Restored);
