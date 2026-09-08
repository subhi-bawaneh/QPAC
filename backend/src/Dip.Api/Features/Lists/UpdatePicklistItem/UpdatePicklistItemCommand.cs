using Dip.Api.Features.Lists.GetPicklists;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Lists.UpdatePicklistItem;

// Editing a code does not rewrite the documents that already carry it (R9): the list
// is the set of values offered from here on, not a foreign key.
[Permission(Permissions.ListsManage)]
public sealed record UpdatePicklistItemCommand(
    Guid Id,
    string Code,
    string Description,
    int SortOrder) : ICommand<PicklistItemDto>;
