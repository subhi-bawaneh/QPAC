using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Lists.GetPicklists;

// Every picklist the project knows, grouped by the field it fills — one group per
// PicklistField even when empty, so the Lists page always has a tab per list.
// Small enough to return whole: the whole point is a screen that shows the allowed
// values at once.
[Permission(Permissions.ReportsView)]
public sealed record GetPicklistsQuery(Guid ProjectId, bool IncludeDeleted = false)
    : IQuery<IReadOnlyList<PicklistGroupDto>>;

public sealed record PicklistGroupDto(PicklistField Field, IReadOnlyList<PicklistItemDto> Items);

public sealed record PicklistItemDto(
    Guid Id,
    PicklistField Field,
    string Code,
    string Description,
    int SortOrder,
    bool IsDeleted,
    DateTime? DeletedAt)
{
    public static PicklistItemDto From(Domain.Entities.PicklistItem p) =>
        new(p.Id, p.Field, p.Code, p.Description, p.SortOrder, p.IsDeleted, p.DeletedAt);
}
