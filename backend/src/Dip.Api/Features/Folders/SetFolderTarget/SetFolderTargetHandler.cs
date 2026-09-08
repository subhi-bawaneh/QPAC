using Dip.Api.Workers;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.SetFolderTarget;

public sealed class SetFolderTargetHandler : ICommandHandler<SetFolderTargetCommand, SetTargetResult>
{
    private readonly DipDbContext _db;
    private readonly WorkQueue _queue;

    public SetFolderTargetHandler(DipDbContext db, WorkQueue queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task<SetTargetResult> Handle(SetFolderTargetCommand command, CancellationToken ct)
    {
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == command.Id && !f.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Folder {command.Id} not found");

        var subtree = await FolderSubtree.LoadAsync(_db, folder.Id, ct);
        var updated = 0;
        foreach (var descendant in subtree)
        {
            if (descendant.Target == command.Target) continue;
            descendant.Target = command.Target;
            updated++;
        }

        // Which layer a file's rows count in changes with the target, so the
        // materialised tracker rows have to be rebuilt.
        var projectId = folder.ProjectId;
        _db.SavedChanges += Enqueue;

        void Enqueue(object? sender, SavedChangesEventArgs args)
        {
            _db.SavedChanges -= Enqueue;
            _queue.EnqueueRecalculate(projectId);
        }

        return new SetTargetResult(folder.Id, command.Target, updated);
    }
}
