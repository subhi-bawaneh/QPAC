using Dip.Api.Features.Folders;
using Dip.Api.Workers;
using Dip.Application.Abstractions;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Drafts.ConvertToLive;

// Everything runs inside the single SaveChanges the TransactionBehavior issues, so a
// half-converted company is not a state the database can be left in.
public sealed class ConvertToLiveHandler : ICommandHandler<ConvertToLiveCommand, ConvertToLiveResultDto>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly WorkQueue _queue;

    public ConvertToLiveHandler(DipDbContext db, ICurrentUser currentUser, WorkQueue queue)
    {
        _db = db;
        _currentUser = currentUser;
        _queue = queue;
    }

    public async Task<ConvertToLiveResultDto> Handle(ConvertToLiveCommand command, CancellationToken ct)
    {
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == command.FolderId && !f.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Folder {command.FolderId} not found");

        var subtree = await FolderSubtree.LoadAsync(_db, folder.Id, ct);
        var folderIds = subtree.Select(f => f.Id).ToList();

        var files = await _db.FolderFiles
            .Where(f => folderIds.Contains(f.FolderId) && !f.IsDeleted)
            .Select(f => new { f.Id, f.Name })
            .ToListAsync(ct);

        var withDrafts = await _db.TidpDrafts
            .Where(d => files.Select(f => f.Id).Contains(d.FolderFileId))
            .Select(d => d.FolderFileId)
            .Distinct()
            .ToListAsync(ct);
        var convertible = withDrafts.ToHashSet();

        var executor = new PromoteExecutor(_db);
        var by = _currentUser.UserName ?? "system";
        var perFile = new List<ConvertedFileDto>();

        foreach (var file in files.Where(f => convertible.Contains(f.Id)).OrderBy(f => f.Name))
        {
            // deleteMissing: converting means "Live now mirrors the Drive file", so a
            // row the file no longer holds must not survive in Live.
            var result = await executor.ExecuteAsync(file.Id, deleteMissing: true, by, ct);
            perFile.Add(new ConvertedFileDto(
                file.Id, file.Name, result.Added, result.Updated, result.Deleted, result.Conflicts));
        }

        foreach (var descendant in subtree)
        {
            descendant.Target = DataTarget.Live;
        }

        var projectId = folder.ProjectId;
        _db.SavedChanges += Enqueue;

        void Enqueue(object? sender, SavedChangesEventArgs args)
        {
            _db.SavedChanges -= Enqueue;
            _queue.EnqueueRecalculate(projectId);
        }

        return new ConvertToLiveResultDto(
            folder.Id,
            perFile.Count,
            perFile.Sum(f => f.Added),
            perFile.Sum(f => f.Updated),
            perFile.Sum(f => f.Deleted),
            files.Count - perFile.Count,
            perFile.Sum(f => f.Conflicts),
            perFile);
    }
}
