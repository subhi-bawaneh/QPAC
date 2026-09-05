using System.Text.Json;
using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Drafts.RollbackPromote;

public sealed class RollbackPromoteHandler
    : ICommandHandler<RollbackPromoteCommand, RollbackPromoteResultDto>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public RollbackPromoteHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<RollbackPromoteResultDto> Handle(
        RollbackPromoteCommand command, CancellationToken ct)
    {
        var batch = await _db.PromoteBatches
            .FirstOrDefaultAsync(b => b.Id == command.PromoteBatchId, ct)
            ?? throw new KeyNotFoundException($"Promote batch {command.PromoteBatchId} not found");

        if (batch.RolledBack)
        {
            throw new FluentValidation.ValidationException(
                $"Promote batch {batch.Id} has already been rolled back");
        }

        var snapshot = JsonSerializer.Deserialize<Api.Features.Drafts.PromoteSnapshot>(batch.SnapshotJson)
            ?? throw new InvalidOperationException($"Promote batch {batch.Id} has no usable snapshot");

        var now = DateTime.UtcNow;
        var by = _currentUser.UserName ?? "system";

        // 1. Rows the promote added -> delete (cascades to exchanges and snapshots).
        var removedIds = new HashSet<Guid>();
        var removed = 0;
        if (snapshot.AddedDocumentIds.Count > 0)
        {
            var added = await _db.Documents
                .Where(d => snapshot.AddedDocumentIds.Contains(d.Id))
                .ToListAsync(ct);
            foreach (var document in added)
            {
                Audit(batch.ProjectId, document.Id, "Delete", document.DocumentNumber, null, by, now);
            }
            _db.Documents.RemoveRange(added);
            foreach (var document in added) removedIds.Add(document.Id);
            removed = added.Count;
        }

        // 2. Rows the promote updated -> restore their pre-promote state.
        var restored = 0;
        if (snapshot.UpdatedBefore.Count > 0)
        {
            var ids = snapshot.UpdatedBefore.Select(u => u.Id).ToList();
            var live = await _db.Documents
                .Include(d => d.Exchanges)
                .Where(d => ids.Contains(d.Id))
                .ToListAsync(ct);
            var liveById = live.ToDictionary(d => d.Id);

            foreach (var before in snapshot.UpdatedBefore)
            {
                if (!liveById.TryGetValue(before.Id, out var document)) continue;
                before.RestoreTo(document);
                Audit(batch.ProjectId, document.Id, "Update", null, before.DocumentNumber, by, now);
                restored++;
            }
        }

        // 3. Rows the promote deleted -> put them back under their original ids.
        var reinserted = 0;
        foreach (var before in snapshot.DeletedBefore)
        {
            var document = new Document { Id = before.Id };
            before.RestoreTo(document);
            _db.Documents.Add(document);
            Audit(batch.ProjectId, document.Id, "Create", null, document.DocumentNumber, by, now);
            reinserted++;
        }

        // 4. A Tidp created by the promote goes too, once nothing references it.
        if (snapshot.CreatedTidpId is Guid tidpId)
        {
            var stillUsed = await _db.Documents
                .AnyAsync(d => d.TidpId == tidpId && !snapshot.AddedDocumentIds.Contains(d.Id), ct);
            if (!stillUsed)
            {
                var tidp = await _db.Tidps.FirstOrDefaultAsync(t => t.Id == tidpId, ct);
                if (tidp is not null) _db.Tidps.Remove(tidp);
            }
        }

        // 5. The drafts of this file are no longer "Unchanged" — recompute them against
        //    the restored Live layer so the file can be reviewed and promoted again.
        var drafts = await _db.DocumentDrafts
            .Include(d => d.Exchanges)
            .Where(d => d.FolderFileId == batch.FolderFileId)
            .ToListAsync(ct);
        if (drafts.Count > 0)
        {
            await DraftRecalculator.RefreshAsync(
                _db, batch.ProjectId, batch.FolderFileId, drafts,
                drafts.Select(d => d.DocumentNumber), ct, removedIds);
        }

        batch.RolledBack = true;

        return new RollbackPromoteResultDto(
            batch.Id, removed, restored, reinserted,
            RecalculationRequired: removed + restored + reinserted > 0);
    }

    private void Audit(
        Guid projectId, Guid documentId, string action,
        string? oldValue, string? newValue, string by, DateTime at)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ProjectId = projectId,
            EntityName = nameof(Document),
            EntityId = documentId,
            Action = action,
            Field = "RollbackPromote",
            OldValue = oldValue,
            NewValue = newValue,
            UserId = by,
            At = at,
        });
    }
}
