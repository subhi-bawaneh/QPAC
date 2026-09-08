using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Lists.DeletePicklistItem;

// Soft delete: the row stays so a re-import cannot bring the code back and so the
// operator can restore it (R9).
[Permission(Permissions.ListsManage)]
public sealed record DeletePicklistItemCommand(Guid Id) : ICommand<Unit>;
