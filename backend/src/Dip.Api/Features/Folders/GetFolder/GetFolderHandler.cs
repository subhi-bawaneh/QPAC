using Dip.Application.Abstractions;
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

        var subfolders = await _db.Folders.AsNoTracking()
            .Where(f => f.ParentId == folder.Id && !f.IsDeleted)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Name)
            .Select(f => new FolderNode(
                f.Id, f.ParentId, f.Name, f.Path, f.Target, f.DriveFolderId, f.LastSyncedAt,
                _db.Folders.Count(c => c.ParentId == f.Id && !c.IsDeleted),
                _db.FolderFiles.Count(x => x.FolderId == f.Id && !x.IsDeleted)))
            .ToListAsync(ct);

        var files = await _db.FolderFiles.AsNoTracking()
            .Where(x => x.FolderId == folder.Id && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new FolderFileDto(
                x.Id, x.Name, x.Kind, x.ContentSource, x.ContentModifiedAt,
                x.DriveFileId, x.DriveModifiedAt, x.SizeBytes,
                x.State, x.ImportError, x.LastImportedAt))
            .ToListAsync(ct);

        var self = new FolderNode(
            folder.Id, folder.ParentId, folder.Name, folder.Path, folder.Target,
            folder.DriveFolderId, folder.LastSyncedAt, subfolders.Count, files.Count);

        return new FolderDetail(self, subfolders, files);
    }
}
