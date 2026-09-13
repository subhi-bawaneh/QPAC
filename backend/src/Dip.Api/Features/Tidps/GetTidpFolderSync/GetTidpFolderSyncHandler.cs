using System.Text.Json;
using Dip.Api.Features.Tidps.SyncTidpFolder;
using Dip.Application.Abstractions;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Tidps.GetTidpFolderSync;

// The stored plan, with the current state of each file laid over it.
//
// The plan is what the sync decided; the TidpFile rows are what became of it. Keeping
// them apart is what lets a workbook that failed to parse in the worker — long after
// the request that queued it returned 202 — still be reported against the sync that
// sent it there, as Failed with the importer's own message.
public sealed class GetTidpFolderSyncHandler
    : IQueryHandler<GetTidpFolderSyncQuery, TidpFolderSyncResult>
{
    private readonly DipDbContext _db;

    public GetTidpFolderSyncHandler(DipDbContext db) => _db = db;

    public async Task<TidpFolderSyncResult> Handle(GetTidpFolderSyncQuery query, CancellationToken ct)
    {
        var sync = await _db.TidpFolderSyncs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == query.SyncId && s.ProjectId == query.ProjectId, ct)
            ?? throw new KeyNotFoundException($"TIDP folder sync {query.SyncId} not found");

        var planned = string.IsNullOrWhiteSpace(sync.ResultJson)
            ? []
            : JsonSerializer.Deserialize<List<TidpFolderSyncFile>>(
                  sync.ResultJson, TidpFolderSyncJson.Options) ?? [];

        var fileIds = planned.Where(f => f.TidpFileId is not null)
            .Select(f => f.TidpFileId!.Value)
            .ToList();

        var live = await _db.TidpFiles
            .AsNoTracking()
            .Where(t => fileIds.Contains(t.Id))
            .Select(t => new
            {
                t.Id, t.Status, t.Error, t.RowsRead, t.RowsImported, t.RowsSkipped, t.FolderStatus,
            })
            .ToDictionaryAsync(t => t.Id, ct);

        var files = new List<TidpFolderSyncFile>(planned.Count);
        foreach (var entry in planned)
        {
            if (entry.TidpFileId is null || !live.TryGetValue(entry.TidpFileId.Value, out var row))
            {
                files.Add(entry);
                continue;
            }

            // A queued file that failed in the worker becomes Failed here, carrying the
            // importer's message. Nothing else is rewritten: a Skipped file is still
            // Skipped whatever its last import did.
            var action = entry.Action is TidpSyncAction.Added or TidpSyncAction.Updated
                         && row.Status == TidpFileStatus.Failed
                ? TidpSyncAction.Failed
                : entry.Action;

            files.Add(entry with
            {
                Action = action,
                Error = action == TidpSyncAction.Failed ? row.Error ?? entry.Error : entry.Error,
                ImportStatus = row.Status,
                RowsRead = row.RowsRead,
                RowsImported = row.RowsImported,
                RowsSkipped = row.RowsSkipped,
            });
        }

        // Recounted from the overlaid rows rather than read off the sync: the whole
        // point of this endpoint is that the numbers move after the 202.
        return new TidpFolderSyncResult(
            sync.Id,
            sync.ProjectId,
            sync.RootName,
            sync.StartedAt,
            files.Count,
            files.Count(f => f.Action == TidpSyncAction.Added),
            files.Count(f => f.Action == TidpSyncAction.Updated),
            files.Count(f => f.Action == TidpSyncAction.Skipped),
            files.Count(f => f.Action == TidpSyncAction.Missing),
            files.Count(f => f.Action == TidpSyncAction.Failed),
            files);
    }
}
