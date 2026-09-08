using Dip.Api.Workers;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Drafts.Promote;

// Thin wrapper over PromoteExecutor so the same code path serves both the
// per-file Promote command and ConvertToLive.
public sealed class PromoteHandler : ICommandHandler<PromoteCommand, PromoteResultDto>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly WorkQueue _queue;

    public PromoteHandler(DipDbContext db, ICurrentUser currentUser, WorkQueue queue)
    {
        _db = db;
        _currentUser = currentUser;
        _queue = queue;
    }

    public async Task<PromoteResultDto> Handle(PromoteCommand command, CancellationToken ct)
    {
        var projectId = await _db.FolderFiles
            .Where(f => f.Id == command.FolderFileId)
            .Select(f => f.Folder!.ProjectId)
            .FirstOrDefaultAsync(ct);

        var result = await new PromoteExecutor(_db).ExecuteAsync(
            command.FolderFileId, command.DeleteMissing, _currentUser.UserName ?? "system", ct);

        if (result.RecalculationRequired && projectId != Guid.Empty)
        {
            _db.SavedChanges += Enqueue;

            void Enqueue(object? sender, SavedChangesEventArgs args)
            {
                _db.SavedChanges -= Enqueue;
                _queue.EnqueueRecalculate(projectId);
            }
        }

        return result;
    }
}
