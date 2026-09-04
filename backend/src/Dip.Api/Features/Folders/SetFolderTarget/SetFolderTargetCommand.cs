using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Folders.SetFolderTarget;

[Permission(Permissions.FoldersAssignTarget)]
public sealed record SetFolderTargetCommand(Guid Id, DataTarget Target) : ICommand<Unit>;
