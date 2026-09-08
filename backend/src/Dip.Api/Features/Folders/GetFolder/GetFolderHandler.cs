using Dip.Application.Abstractions;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.GetFolder;

public sealed class GetFolderHandler : IQueryHandler<GetFolderQuery, FolderDetail>
{
    private readonly DipDbContext _db;

    public GetFolderHandler(DipDbContext db) => _db = db;

    public async Task<FolderDetail> Handle(GetFolderQuery query, CancellationToken ct)
    {
        var folder = await _db.Folders.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == query.Id && !f.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Folder {query.Id} not found");

        var files = await _db.FolderFiles.AsNoTracking()
            .Where(x => x.FolderId == folder.Id && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var facts = await FolderProjections.LoadAsync(
            _db, folder.ProjectId, files.Select(f => f.Id).ToList(), ct);

        var fileDtos = files
            .Select(x => new FolderFileDto(
                x.Id, x.Name, x.Kind, x.ContentSource, x.ContentModifiedAt,
                x.DriveFileId, x.DriveModifiedAt, x.SizeBytes,
                x.State, x.ImportError, x.LastImportedAt,
                facts.RowCounts.TryGetValue(x.Id, out var rows) ? rows : null,
                facts.WithNewerDraft.Contains(x.Id),
                folder.Target))
            .ToList();

        var subfolders = await _db.Folders.AsNoTracking()
            .Where(f => f.ParentId == folder.Id && !f.IsDeleted)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Name)
            .Select(f => new
            {
                Folder = f,
                AuthorName = f.Author != null ? f.Author.Code : null,
                ChildCount = _db.Folders.Count(c => c.ParentId == f.Id && !c.IsDeleted),
                FileCount = _db.FolderFiles.Count(x => x.FolderId == f.Id && !x.IsDeleted),
            })
            .ToListAsync(ct);

        var subfolderNodes = new List<FolderNode>(subfolders.Count);
        foreach (var row in subfolders)
        {
            subfolderNodes.Add(new FolderNode(
                row.Folder.Id, row.Folder.ParentId, row.Folder.Name, row.Folder.Path, row.Folder.Target,
                row.Folder.DriveFolderId, row.Folder.LastSyncedAt,
                row.Folder.IsCompany, row.Folder.AuthorId, row.AuthorName,
                row.ChildCount, row.FileCount,
                await HasNewerDraftBelowAsync(row.Folder.Id, ct)));
        }

        var authorName = folder.AuthorId is null
            ? null
            : await _db.PicklistItems.AsNoTracking()
                .Where(p => p.Id == folder.AuthorId.Value)
                .Select(p => p.Code)
                .FirstOrDefaultAsync(ct);

        var self = new FolderNode(
            folder.Id, folder.ParentId, folder.Name, folder.Path, folder.Target,
            folder.DriveFolderId, folder.LastSyncedAt, folder.IsCompany, folder.AuthorId, authorName,
            subfolderNodes.Count, fileDtos.Count,
            fileDtos.Any(f => f.HasNewerDraft) || subfolderNodes.Any(f => f.HasNewerDraft));

        return new FolderDetail(self, subfolderNodes, fileDtos);
    }

    private async Task<bool> HasNewerDraftBelowAsync(Guid folderId, CancellationToken ct)
    {
        var subtree = await FolderSubtree.LoadIdsAsync(_db, folderId, ct);
        var fileIds = await _db.FolderFiles.AsNoTracking()
            .Where(f => subtree.Contains(f.FolderId) && !f.IsDeleted && f.Folder!.Target == DataTarget.Live)
            .Select(f => f.Id)
            .ToListAsync(ct);
        if (fileIds.Count == 0) return false;

        var projectId = await _db.Folders.AsNoTracking()
            .Where(f => f.Id == folderId).Select(f => f.ProjectId).FirstAsync(ct);
        var facts = await FolderProjections.LoadAsync(_db, projectId, fileIds, ct);
        return facts.WithNewerDraft.Count > 0;
    }
}
