using Dip.Api.Features.Lists.GetPicklists;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Lists.RestorePicklistItem;

[Permission(Permissions.ListsManage)]
public sealed record RestorePicklistItemCommand(Guid Id) : ICommand<PicklistItemDto>;
