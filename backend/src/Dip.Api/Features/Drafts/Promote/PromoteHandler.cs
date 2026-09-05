using System.Text.Json;
using Dip.Application.Abstractions;
using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Drafts.Promote;

// Draft -> Live. Everything happens in the single SaveChanges the TransactionBehavior
// issues after this handler returns, so the whole promote commits or none of it does.
public sealed class PromoteHandler : ICommandHandler<PromoteCommand, PromoteResultDto>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public PromoteHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PromoteResultDto> Handle(PromoteCommand command, CancellationToken ct)
    {
        var context = await PromoteLoader.LoadAsync(_db, command.FolderFileId, ct);
        var now = DateTime.UtcNow;
        var by = _currentUser.UserName ?? "system";

        var draftsById = context.Drafts.ToDictionary(d => d.Id);
        var (tidp, createdTidpId) = await UpsertTidpAsync(context, now, by, ct);

        var addedIds = new List<Guid>();
        var updatedBefore = new List<PromotedDocumentState>();
        var deletedBefore = new List<PromotedDocumentState>();
        var touchedDocumentIds = new List<Guid>();
        var skipped = 0;
        var conflicts = 0;

        foreach (var decision in context.Decisions)
        {
            switch (decision.Action)
            {
                case PromoteAction.Add:
                {
                    var draft = draftsById[decision.DraftId!.Value];
                    var document = new Document { ProjectId = context.ProjectId };
                    ApplyDraft(document, draft, tidp.Id, context.FolderFileId, now, by, isNew: true);
                    _db.Documents.Add(document);
                    addedIds.Add(document.Id);
                    touchedDocumentIds.Add(document.Id);
                    Audit(context.ProjectId, document.Id, "Create", null, null, document.DocumentNumber, by, now);
                    draft.State = DraftRowState.Unchanged;
                    draft.LiveDocumentId = document.Id;
                    draft.ConflictReason = null;
                    break;
                }

                case PromoteAction.Update:
                {
                    var draft = draftsById[decision.DraftId!.Value];
                    var document = context.LiveById[decision.LiveId!.Value];
                    updatedBefore.Add(PromotedDocumentState.From(document));

                    foreach (var change in decision.Changes)
                    {
                        Audit(context.ProjectId, document.Id, "Update",
                            change.Field, change.OldValue, change.NewValue, by, now);
                    }

                    ApplyDraft(document, draft, tidp.Id, context.FolderFileId, now, by, isNew: false);
                    touchedDocumentIds.Add(document.Id);
                    draft.State = DraftRowState.Unchanged;
                    draft.LiveDocumentId = document.Id;
                    draft.ConflictReason = null;
                    break;
                }

                case PromoteAction.Delete:
                {
                    var document = context.LiveById[decision.LiveId!.Value];
                    if (!command.DeleteMissing)
                    {
                        skipped++;
                        break;
                    }

                    deletedBefore.Add(PromotedDocumentState.From(document));
                    Audit(context.ProjectId, document.Id, "Delete", null, document.DocumentNumber, null, by, now);
                    _db.Documents.Remove(document);
                    touchedDocumentIds.Add(document.Id);
                    break;
                }

                case PromoteAction.Conflict:
                {
                    conflicts++;
                    if (decision.DraftId is not null)
                    {
                        var draft = draftsById[decision.DraftId.Value];
                        draft.State = DraftRowState.Conflict;
                        draft.ConflictReason = decision.Reason;
                    }
                    break;
                }

                case PromoteAction.Unchanged:
                default:
                {
                    skipped++;
                    if (decision.DraftId is not null)
                    {
                        var draft = draftsById[decision.DraftId.Value];
                        draft.State = DraftRowState.Unchanged;
                        draft.LiveDocumentId = decision.LiveId;
                        draft.ConflictReason = null;
                    }
                    break;
                }
            }
        }

        var snapshot = new PromoteSnapshot(
            context.FolderFileId, now, addedIds, updatedBefore, deletedBefore, createdTidpId);

        var batch = new PromoteBatch
        {
            ProjectId = context.ProjectId,
            FolderId = await FolderIdAsync(context.FolderFileId, ct),
            FolderFileId = context.FolderFileId,
            At = now,
            By = by,
            Added = addedIds.Count,
            Updated = updatedBefore.Count,
            Deleted = deletedBefore.Count,
            Skipped = skipped,
            SnapshotJson = JsonSerializer.Serialize(snapshot),
            RolledBack = false,
        };
        _db.PromoteBatches.Add(batch);

        // Engine output for the touched documents is now stale; Phase 5.6's
        // RecalculationService rebuilds it, so drop it rather than leave it wrong.
        if (touchedDocumentIds.Count > 0)
        {
            // Loaded and removed through the change tracker (not ExecuteDelete) so it
            // lands in the same transaction as everything else in this promote.
            var stale = await _db.DocumentSnapshots
                .Where(s => touchedDocumentIds.Contains(s.DocumentId))
                .ToListAsync(ct);
            _db.DocumentSnapshots.RemoveRange(stale);
        }

        return new PromoteResultDto(
            batch.Id, context.FolderFileId,
            batch.Added, batch.Updated, batch.Deleted, batch.Skipped, conflicts,
            RecalculationRequired: touchedDocumentIds.Count > 0);
    }

    // The Live Tidp row for this discipline, created from the TidpDraft if absent.
    private async Task<(Tidp Tidp, Guid? CreatedId)> UpsertTidpAsync(
        PromoteContext context, DateTime now, string by, CancellationToken ct)
    {
        var draft = context.TidpDraft;
        var existing = await _db.Tidps.FirstOrDefaultAsync(
            t => t.ProjectId == draft.ProjectId && t.DisciplineId == draft.DisciplineId, ct);

        if (existing is not null)
        {
            existing.FolderFileId = context.FolderFileId;
            existing.DocumentReference = draft.DocumentReference;
            existing.RevisionNumber = draft.RevisionNumber;
            existing.DateCreated = draft.DateCreated ?? existing.DateCreated;
            existing.DateLastUpdated = draft.DateLastUpdated ?? existing.DateLastUpdated;
            existing.UpdatedAt = now;
            existing.UpdatedBy = by;
            return (existing, null);
        }

        var created = new Tidp
        {
            ProjectId = draft.ProjectId,
            DisciplineId = draft.DisciplineId,
            FolderFileId = context.FolderFileId,
            DocumentReference = draft.DocumentReference,
            RevisionNumber = draft.RevisionNumber,
            DateCreated = draft.DateCreated,
            DateLastUpdated = draft.DateLastUpdated,
            SourceFileName = draft.SourceFileName,
            CreatedAt = now,
            CreatedBy = by,
            UpdatedAt = now,
            UpdatedBy = by,
        };
        _db.Tidps.Add(created);
        return (created, created.Id);
    }

    private async Task<Guid> FolderIdAsync(Guid folderFileId, CancellationToken ct) =>
        await _db.FolderFiles
            .Where(f => f.Id == folderFileId)
            .Select(f => f.FolderId)
            .FirstOrDefaultAsync(ct);

    private static void ApplyDraft(
        Document document, DocumentDraft draft, Guid tidpId, Guid folderFileId,
        DateTime now, string by, bool isNew)
    {
        document.TidpId = tidpId;
        document.DisciplineId = draft.DisciplineId;
        document.FolderFileId = folderFileId;
        document.DocumentNumber = draft.DocumentNumber;
        document.Title = draft.Title;
        document.ExtractedFromModel = draft.ExtractedFromModel;
        document.ScopeArea = draft.ScopeArea;
        document.AuthoringSoftware = draft.AuthoringSoftware;
        document.ExchangeFormat = draft.ExchangeFormat;
        document.Scale = draft.Scale;
        document.DeliveryMilestone = draft.DeliveryMilestone;
        document.PackageName = draft.PackageName;
        document.ActivityId = draft.ActivityId;
        document.ClassificationCode = draft.ClassificationCode;
        document.F01Project = draft.F01Project;
        document.F02Originator = draft.F02Originator;
        document.F03Contract = draft.F03Contract;
        document.F04DocType = draft.F04DocType;
        document.F05Discipline = draft.F05Discipline;
        document.F06Zone = draft.F06Zone;
        document.F07Building = draft.F07Building;
        document.F08ADrawingType = draft.F08ADrawingType;
        document.F08BLevel = draft.F08BLevel;
        document.F08CSequence = draft.F08CSequence;
        document.CorporateDiscipline = draft.CorporateDiscipline;
        document.BudgetWeight = draft.BudgetWeight;

        if (isNew)
        {
            document.CreatedAt = now;
            document.CreatedBy = by;
        }
        document.UpdatedAt = now;
        document.UpdatedBy = by;

        ApplyExchanges(document, draft);
    }

    private static void ApplyExchanges(Document document, DocumentDraft draft)
    {
        foreach (var number in document.Exchanges.Select(e => e.Number).ToList())
        {
            if (draft.Exchanges.All(e => e.Number != number))
            {
                document.Exchanges.Remove(document.Exchanges.First(e => e.Number == number));
            }
        }

        foreach (var source in draft.Exchanges)
        {
            var target = document.Exchanges.FirstOrDefault(e => e.Number == source.Number);
            if (target is null)
            {
                target = new DataExchange { Number = source.Number };
                document.Exchanges.Add(target);
            }

            target.Stage = source.Stage;
            target.ProgrammeRef = source.ProgrammeRef;
            target.Author = source.Author;
            target.Geometrical = source.Geometrical;
            target.NonGeometrical = source.NonGeometrical;
            target.DurationDays = source.DurationDays;
            target.Predecessor = source.Predecessor;
            target.ExchangeDate = source.ExchangeDate;
        }
    }

    private void Audit(
        Guid projectId, Guid documentId, string action, string? field,
        string? oldValue, string? newValue, string by, DateTime at)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ProjectId = projectId,
            EntityName = nameof(Document),
            EntityId = documentId,
            Action = action,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            UserId = by,
            At = at,
        });
    }
}
