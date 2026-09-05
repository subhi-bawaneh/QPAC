using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Drafts;

// Everything GetPromoteDiff and Promote both need: the draft rows of one file, the
// Live rows they touch, and the classification of each. Loaded once, in one place,
// so the preview and the write can never disagree about what would happen.
internal sealed record PromoteContext(
    Guid ProjectId,
    Guid FolderFileId,
    TidpDraft TidpDraft,
    Guid ImportBatchId,
    DateTime ImportedAt,
    DateTime Baseline,
    IReadOnlyList<DocumentDraft> Drafts,
    IReadOnlyDictionary<Guid, Document> LiveById,
    IReadOnlyList<PromoteDecision> Decisions);

internal static class PromoteLoader
{
    public static async Task<PromoteContext> LoadAsync(
        DipDbContext db, Guid folderFileId, CancellationToken ct)
    {
        var tidpDraft = await db.TidpDrafts
            .FirstOrDefaultAsync(t => t.FolderFileId == folderFileId, ct)
            ?? throw new KeyNotFoundException(
                $"No draft has been imported for file {folderFileId}");

        var drafts = await db.DocumentDrafts
            .Include(d => d.Exchanges)
            .Where(d => d.FolderFileId == folderFileId)
            .ToListAsync(ct);

        var importedAt = await db.ImportBatches
            .Where(b => b.Id == tidpDraft.ImportBatchId)
            .Select(b => (DateTime?)b.ImportedAt)
            .FirstOrDefaultAsync(ct) ?? tidpDraft.CreatedAt;

        // "Live changed after the draft was imported" must not fire on changes THIS
        // file's own earlier promote made — those rows carry that promote's timestamp.
        var lastPromotedAt = await db.PromoteBatches
            .Where(b => b.FolderFileId == folderFileId && !b.RolledBack)
            .OrderByDescending(b => b.At)
            .Select(b => (DateTime?)b.At)
            .FirstOrDefaultAsync(ct);
        var baseline = lastPromotedAt is null || lastPromotedAt <= importedAt
            ? importedAt
            : lastPromotedAt.Value;

        // Live rows this promote could touch: same number as a draft row, or already
        // owned by this file (those are the deletion candidates).
        var upperNumbers = drafts
            .Select(d => d.DocumentNumber.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var live = await db.Documents
            .Include(d => d.Exchanges)
            .Where(d => d.ProjectId == tidpDraft.ProjectId
                && (d.FolderFileId == folderFileId || upperNumbers.Contains(d.DocumentNumber.ToUpper())))
            .ToListAsync(ct);

        var decisions = PromoteClassifier.Classify(
            drafts.Select(ToDraftRow),
            live.Select(ToLiveRow),
            folderFileId,
            baseline);

        return new PromoteContext(
            tidpDraft.ProjectId, folderFileId, tidpDraft, tidpDraft.ImportBatchId, importedAt, baseline,
            drafts, live.ToDictionary(d => d.Id), decisions);
    }

    private static PromoteDraftRow ToDraftRow(DocumentDraft d) => new(
        d.Id, d.DocumentNumber, d.IsDuplicate, DraftDiff.From(d), ExchangeSignature.Of(d.Exchanges));

    private static PromoteLiveRow ToLiveRow(Document d) => new(
        d.Id, d.DocumentNumber, d.FolderFileId, d.UpdatedAt, DraftDiff.From(d),
        ExchangeSignature.Of(d.Exchanges));
}
