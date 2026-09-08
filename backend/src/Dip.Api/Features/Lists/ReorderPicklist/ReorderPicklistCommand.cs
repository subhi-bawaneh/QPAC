using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Lists.ReorderPicklist;

// The ids in the order the operator dragged them into; SortOrder becomes 1..n.
[Permission(Permissions.ListsManage)]
public sealed record ReorderPicklistCommand(
    Guid ProjectId,
    PicklistField Field,
    IReadOnlyList<Guid> Ids) : ICommand<Unit>;
