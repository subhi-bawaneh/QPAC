using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Folders.SetFolderTarget;

// The target belongs to the folder and cascades to everything under it
// (refactor-plan § 3 R6), so a company is switched between the two layers in one move.
[Permission(Permissions.FoldersAssignTarget)]
public sealed record SetFolderTargetCommand(Guid Id, DataTarget Target) : ICommand<SetTargetResult>;

public sealed record SetTargetResult(Guid FolderId, DataTarget Target, int FoldersUpdated);
