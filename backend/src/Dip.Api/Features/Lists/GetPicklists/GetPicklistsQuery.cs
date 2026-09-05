using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Lists.GetPicklists;

// Every picklist the project knows, grouped by the field it fills. Small enough to
// return whole — the whole point is a screen that shows the allowed values at once.
[Permission(Permissions.ReportsView)]
public sealed record GetPicklistsQuery(Guid ProjectId) : IQuery<IReadOnlyList<PicklistGroupDto>>;

public sealed record PicklistGroupDto(PicklistField Field, IReadOnlyList<PicklistItemDto> Items);

public sealed record PicklistItemDto(Guid Id, string Code, string Description, int SortOrder);
