using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders;

// The folder plus every non-deleted folder below it, loaded tracked so the caller can
// cascade a change over the whole branch (refactor-plan § 3 R6/R7).
internal static class FolderSubtree
{
    public static async Task<List<Folder>> LoadAsync(DipDbContext db, Guid rootId, CancellationToken ct)
    {
        var root = await db.Folders.FirstOrDefaultAsync(f => f.Id == rootId && !f.IsDeleted, ct);
        if (root is null) return [];

        var all = new List<Folder> { root };
        var frontier = new List<Guid> { rootId };

        while (frontier.Count > 0)
        {
            var children = await db.Folders
                .Where(f => f.ParentId != null && frontier.Contains(f.ParentId.Value) && !f.IsDeleted)
                .ToListAsync(ct);
            if (children.Count == 0) break;

            all.AddRange(children);
            frontier = children.Select(f => f.Id).ToList();
        }

        return all;
    }

    public static async Task<List<Guid>> LoadIdsAsync(DipDbContext db, Guid rootId, CancellationToken ct)
    {
        var ids = new List<Guid> { rootId };
        var frontier = new List<Guid> { rootId };

        while (frontier.Count > 0)
        {
            var children = await db.Folders
                .AsNoTracking()
                .Where(f => f.ParentId != null && frontier.Contains(f.ParentId.Value) && !f.IsDeleted)
                .Select(f => f.Id)
                .ToListAsync(ct);
            if (children.Count == 0) break;

            ids.AddRange(children);
            frontier = children;
        }

        return ids;
    }
}
