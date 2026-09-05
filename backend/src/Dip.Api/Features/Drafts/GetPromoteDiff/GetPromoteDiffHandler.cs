using Dip.Application.Abstractions;
using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;

namespace Dip.Api.Features.Drafts.GetPromoteDiff;

public sealed class GetPromoteDiffHandler : IQueryHandler<GetPromoteDiffQuery, PromoteDiffDto>
{
    private readonly DipDbContext _db;

    public GetPromoteDiffHandler(DipDbContext db) => _db = db;

    public async Task<PromoteDiffDto> Handle(GetPromoteDiffQuery query, CancellationToken ct)
    {
        var context = await PromoteLoader.LoadAsync(_db, query.FolderFileId, ct);

        var draftTitles = context.Drafts.ToDictionary(d => d.Id, d => d.Title);
        var decisions = context.Decisions;

        int Count(PromoteAction action) => decisions.Count(d => d.Action == action);

        List<PromoteRowDto> Rows(PromoteAction action) => decisions
            .Where(d => d.Action == action)
            .Take(query.MaxRows)
            .Select(d => new PromoteRowDto(
                d.DocumentNumber, d.DraftId, d.LiveId,
                Title(d, draftTitles, context.LiveById),
                d.Changes, d.Reason))
            .ToList();

        return new PromoteDiffDto(
            context.FolderFileId,
            context.ImportBatchId,
            context.ImportedAt,
            Added: Count(PromoteAction.Add),
            Modified: Count(PromoteAction.Update),
            Unchanged: Count(PromoteAction.Unchanged),
            Deleted: Count(PromoteAction.Delete),
            Conflicts: Count(PromoteAction.Conflict),
            MaxRows: query.MaxRows,
            AddedRows: Rows(PromoteAction.Add),
            ModifiedRows: Rows(PromoteAction.Update),
            DeletedRows: Rows(PromoteAction.Delete),
            ConflictRows: Rows(PromoteAction.Conflict));
    }

    private static string? Title(
        PromoteDecision decision,
        IReadOnlyDictionary<Guid, string> draftTitles,
        IReadOnlyDictionary<Guid, Document> liveById)
    {
        if (decision.DraftId is not null && draftTitles.TryGetValue(decision.DraftId.Value, out var title))
        {
            return title;
        }
        return decision.LiveId is not null && liveById.TryGetValue(decision.LiveId.Value, out var live)
            ? live.Title
            : null;
    }
}
