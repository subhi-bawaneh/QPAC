using Dip.Application.Abstractions;
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
            .ToListAsync(ct);

        var byParent = folders.ToLookup(f => f.ParentId);

        List<FolderTreeNode> Build(Guid? parentId) => byParent[parentId]
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new FolderTreeNode(f.Id, f.ParentId, f.Name, f.Path, f.Target, Build(f.Id)))
            .ToList();

        return Build(null);
    }
}
