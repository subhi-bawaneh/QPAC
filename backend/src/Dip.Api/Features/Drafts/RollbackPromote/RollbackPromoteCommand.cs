using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Drafts.RollbackPromote;

// Restores the Live rows a promote changed, from PromoteBatch.SnapshotJson.
// A batch can only be rolled back once.
[Permission(Permissions.DraftsPromote)]
public sealed record RollbackPromoteCommand(Guid PromoteBatchId) : ICommand<RollbackPromoteResultDto>;

public sealed record RollbackPromoteResultDto(
    Guid PromoteBatchId,
    int Removed,
    int Restored,
    int Reinserted,
    bool RecalculationRequired);
