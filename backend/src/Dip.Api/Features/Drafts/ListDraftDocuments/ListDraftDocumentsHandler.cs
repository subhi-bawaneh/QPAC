using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Drafts.ListDraftDocuments;

public sealed class ListDraftDocumentsHandler
    : IQueryHandler<ListDraftDocumentsQuery, PagedResult<DraftDocumentDto>>
{
    private readonly DipDbContext _db;

    public ListDraftDocumentsHandler(DipDbContext db) => _db = db;

    public async Task<PagedResult<DraftDocumentDto>> Handle(
        ListDraftDocumentsQuery query, CancellationToken ct)
    {
        var q = _db.DocumentDrafts
            .AsNoTracking()
            .Where(d => d.FolderFileId == query.FolderFileId);

        if (query.State is not null) q = q.Where(d => d.State == query.State.Value);
        if (query.DisciplineId is not null) q = q.Where(d => d.DisciplineId == query.DisciplineId.Value);
        if (query.IsDuplicate is not null) q = q.Where(d => d.IsDuplicate == query.IsDuplicate.Value);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{Escape(search)}%";
            q = q.Where(d => EF.Functions.ILike(d.DocumentNumber, pattern, @"\")
                          || EF.Functions.ILike(d.Title, pattern, @"\"));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .Include(d => d.Exchanges)
            .OrderBy(d => d.DocumentNumber)
            .ThenBy(d => d.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<DraftDocumentDto>(
            rows.Select(DraftDocumentDto.From).ToList(), query.Page, query.PageSize, total);
    }

    // LIKE wildcards typed by the user must match literally.
    private static string Escape(string value) => value
        .Replace(@"\", @"\\", StringComparison.Ordinal)
        .Replace("%", @"\%", StringComparison.Ordinal)
        .Replace("_", @"\_", StringComparison.Ordinal);
}
