using Dip.Api.Features.Lists.GetPicklists;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Lists.CreatePicklistItem;

// Creating a code that exists soft-deleted restores that row instead of inserting a
// second one (refactor-plan § 3 R9), so the history of the code is not forked.
[Permission(Permissions.ListsManage)]
public sealed record CreatePicklistItemCommand(
    Guid ProjectId,
    PicklistField Field,
    string Code,
    string Description,
    int? SortOrder) : ICommand<CreatePicklistItemResult>;

public sealed record CreatePicklistItemResult(PicklistItemDto Item, bool Restored);
