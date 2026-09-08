using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders;

// "Drive has newer data": the folder has been converted to Live, yet the file's draft
// was re-imported after the last promote — so Drive is holding rows the Live layer has
// not taken yet and Convert can be run again (refactor-plan § 5.2).
internal sealed record FileFacts(
    IReadOnlySet<Guid> WithNewerDraft,
    IReadOnlyDictionary<Guid, int> RowCounts);

internal static class FolderProjections
{
    public static async Task<FileFacts> LoadAsync(
        DipDbContext db, Guid projectId, IReadOnlyCollection<Guid> fileIds, CancellationToken ct)
    {
        if (fileIds.Count == 0)
        {
            return new FileFacts(new HashSet<Guid>(), new Dictionary<Guid, int>());
        }

        var ids = fileIds.ToList();

        var draftImports = await db.TidpDrafts
            .AsNoTracking()
            .Where(d => ids.Contains(d.FolderFileId))
            .Join(db.ImportBatches.AsNoTracking(), d => d.ImportBatchId, b => b.Id,
                (d, b) => new { d.FolderFileId, b.ImportedAt })
            .ToListAsync(ct);

        var lastPromotes = await db.PromoteBatches
            .AsNoTracking()
            .Where(b => ids.Contains(b.FolderFileId))
            .GroupBy(b => b.FolderFileId)
            .Select(g => new { FolderFileId = g.Key, At = g.Max(b => b.At) })
            .ToListAsync(ct);
        var promotedAt = lastPromotes.ToDictionary(p => p.FolderFileId, p => p.At);

        var liveTargets = await db.FolderFiles
            .AsNoTracking()
            .Where(f => ids.Contains(f.Id) && f.Folder!.Target == DataTarget.Live)
            .Select(f => f.Id)
            .ToListAsync(ct);
        var live = liveTargets.ToHashSet();

        var withNewerDraft = draftImports
            .Where(d => live.Contains(d.FolderFileId)
                && (!promotedAt.TryGetValue(d.FolderFileId, out var at) || d.ImportedAt > at))
            .Select(d => d.FolderFileId)
            .ToHashSet();

        var draftCounts = await db.DocumentDrafts
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId && ids.Contains(d.FolderFileId))
            .GroupBy(d => d.FolderFileId)
            .Select(g => new { FolderFileId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var liveCounts = await db.Documents
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId && d.FolderFileId != null && ids.Contains(d.FolderFileId.Value))
            .GroupBy(d => d.FolderFileId!.Value)
            .Select(g => new { FolderFileId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // A file shows the count of the layer its folder currently targets.
        var rowCounts = new Dictionary<Guid, int>();
        foreach (var row in draftCounts)
        {
            if (!live.Contains(row.FolderFileId)) rowCounts[row.FolderFileId] = row.Count;
        }
        foreach (var row in liveCounts)
        {
            if (live.Contains(row.FolderFileId)) rowCounts[row.FolderFileId] = row.Count;
        }

        return new FileFacts(withNewerDraft, rowCounts);
    }
}
