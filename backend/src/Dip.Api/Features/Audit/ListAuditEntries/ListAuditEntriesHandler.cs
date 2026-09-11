using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Audit.ListAuditEntries;

public sealed class ListAuditEntriesHandler
    : IQueryHandler<ListAuditEntriesQuery, PagedResult<AuditEntryDto>>
{
    private readonly DipDbContext _db;

    public ListAuditEntriesHandler(DipDbContext db) => _db = db;

    public async Task<PagedResult<AuditEntryDto>> Handle(
        ListAuditEntriesQuery query, CancellationToken ct)
    {
        var rows = _db.AuditLogs.AsNoTracking().Where(a => a.ProjectId == query.ProjectId);

        if (!string.IsNullOrWhiteSpace(query.Entity))
        {
            rows = rows.Where(a => a.EntityName == query.Entity);
        }
        if (query.EntityId is { } entityId)
        {
            rows = rows.Where(a => a.EntityId == entityId);
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var total = await rows.CountAsync(ct);

        // Newest first: a history panel is read from the top.
        var items = await rows
            .OrderByDescending(a => a.At)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditEntryDto(
                a.Id, a.EntityName, a.EntityId, a.Action, a.Field,
                a.OldValue, a.NewValue, a.UserId, a.At))
            .ToListAsync(ct);

        return new PagedResult<AuditEntryDto>(items, page, pageSize, total);
    }
}
