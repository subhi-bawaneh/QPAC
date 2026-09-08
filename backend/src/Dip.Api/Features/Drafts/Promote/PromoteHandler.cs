using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;

namespace Dip.Api.Features.Drafts.Promote;

// Thin wrapper over PromoteExecutor so the same code path serves both the
// per-file Promote command and ConvertToLive.
public sealed class PromoteHandler : ICommandHandler<PromoteCommand, PromoteResultDto>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public PromoteHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public Task<PromoteResultDto> Handle(PromoteCommand command, CancellationToken ct) =>
        new PromoteExecutor(_db).ExecuteAsync(
            command.FolderFileId, command.DeleteMissing, _currentUser.UserName ?? "system", ct);
}
