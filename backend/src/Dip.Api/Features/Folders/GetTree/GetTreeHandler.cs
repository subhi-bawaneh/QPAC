using Dip.Application.Abstractions;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.GetTree;

public sealed class GetTreeHandler : IQueryHandler<GetTreeQuery, IReadOnlyList<FolderTreeNode>>
{
    private readonly DipDbContext _db;

    public GetTreeHandler(DipDbContext db) => _db = db;

    public async Task<IReadOnlyList<FolderTreeNode>> Handle(GetTreeQuery query, CancellationToken ct)
    {
        var folders = await _db.Folders.AsNoTracking()
            .Where(f => f.ProjectId == query.ProjectId && !f.IsDeleted)
            .OrderBy(f => f.Path)
            .Select(f => new
            {
                f.Id,
                f.ParentId,
                f.Name,
                f.Path,
                f.Target,
                f.IsCompany,
                f.SortOrder,
                AuthorName = f.Author != null ? f.Author.Code : null,
            })
            .ToListAsync(ct);

        var files = await _db.FolderFiles.AsNoTracking()
            .Where(f => !f.IsDeleted && f.Folder!.ProjectId == query.ProjectId && !f.Folder.IsDeleted)
            .Select(f => new { f.Id, f.FolderId, f.Folder!.Target })
            .ToListAsync(ct);

        var fileCountByFolder = files
            .GroupBy(f => f.FolderId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Only Live-target files can carry a newer draft, so that is all we ask about.
        var liveFileIds = files.Where(f => f.Target == DataTarget.Live).Select(f => f.Id).ToList();
        var facts = await FolderProjections.LoadAsync(_db, query.ProjectId, liveFileIds, ct);
        var newerDraftFolders = files
            .Where(f => facts.WithNewerDraft.Contains(f.Id))
            .Select(f => f.FolderId)
            .ToHashSet();

        var byParent = folders.ToLookup(f => f.ParentId);

        // A folder shows the dot when anything below it does, so the badge survives a
        // collapsed tree.
        List<FolderTreeNode> Build(Guid? parentId) => byParent[parentId]
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f =>
            {
                var children = Build(f.Id);
                return new FolderTreeNode(
                    f.Id, f.ParentId, f.Name, f.Path, f.Target, f.IsCompany, f.AuthorName,
                    fileCountByFolder.TryGetValue(f.Id, out var count) ? count : 0,
                    newerDraftFolders.Contains(f.Id) || children.Any(c => c.HasNewerDraft),
                    children);
            })
            .ToList();

        return Build(null);
    }
}
