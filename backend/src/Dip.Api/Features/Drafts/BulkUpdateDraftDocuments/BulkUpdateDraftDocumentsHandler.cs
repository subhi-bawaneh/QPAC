using Dip.Application.Abstractions;
using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Drafts.BulkUpdateDraftDocuments;

public sealed class BulkUpdateDraftDocumentsHandler
    : ICommandHandler<BulkUpdateDraftDocumentsCommand, int>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public BulkUpdateDraftDocumentsHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(BulkUpdateDraftDocumentsCommand command, CancellationToken ct)
    {
        var ids = command.Ids.Distinct().ToList();
        var drafts = await _db.DocumentDrafts
            .Where(d => ids.Contains(d.Id))
            .ToListAsync(ct);

        var missing = ids.Count - drafts.Count;
        if (missing > 0)
        {
            throw new KeyNotFoundException($"{missing} of {ids.Count} draft document(s) not found");
        }

        foreach (var draft in drafts)
        {
            DraftScope.EnsureCanEdit(_currentUser, draft.F05Discipline);
        }

        var widths = SerialWidths.Create(
            await _db.DocumentTypeSerials.Where(s => !s.IsDeleted).ToListAsync(ct));

        var now = DateTime.UtcNow;
        var by = _currentUser.UserName ?? "system";
        var previousNumbers = new Dictionary<Guid, string>(drafts.Count);

        foreach (var draft in drafts)
        {
            Apply(draft, command.Fields);
            draft.UpdatedAt = now;
            draft.UpdatedBy = by;
            previousNumbers[draft.Id] = DraftRecalculator.ApplyNumber(draft, widths);
        }

        // Rows can span several uploaded files; each file's duplicate flags are its own.
        foreach (var group in drafts.GroupBy(d => new { d.ProjectId, d.FolderFileId }))
        {
            var rows = group.ToList();
            await DraftRecalculator.RefreshAsync(
                _db, group.Key.ProjectId, group.Key.FolderFileId,
                rows, rows.Select(r => previousNumbers[r.Id]), ct);
        }

        return drafts.Count;
    }

    private static void Apply(DocumentDraft draft, BulkDraftFields f)
    {
        if (f.ExtractedFromModel is not null) draft.ExtractedFromModel = f.ExtractedFromModel;
        if (f.ScopeArea is not null) draft.ScopeArea = f.ScopeArea;
        if (f.AuthoringSoftware is not null) draft.AuthoringSoftware = f.AuthoringSoftware;
        if (f.ExchangeFormat is not null) draft.ExchangeFormat = f.ExchangeFormat;
        if (f.Scale is not null) draft.Scale = f.Scale;
        if (f.DeliveryMilestone is not null) draft.DeliveryMilestone = f.DeliveryMilestone;
        if (f.PackageName is not null) draft.PackageName = f.PackageName;
        if (f.ActivityId is not null) draft.ActivityId = f.ActivityId;
        if (f.ClassificationCode is not null) draft.ClassificationCode = f.ClassificationCode;
        if (f.CorporateDiscipline is not null) draft.CorporateDiscipline = f.CorporateDiscipline;
        if (f.F02Originator is not null) draft.F02Originator = f.F02Originator;
        if (f.F03Contract is not null) draft.F03Contract = f.F03Contract;
        if (f.F04DocType is not null) draft.F04DocType = f.F04DocType;
        if (f.F05Discipline is not null) draft.F05Discipline = f.F05Discipline;
        if (f.F06Zone is not null) draft.F06Zone = f.F06Zone;
        if (f.F07Building is not null) draft.F07Building = f.F07Building;
        if (f.F08ADrawingType is not null) draft.F08ADrawingType = f.F08ADrawingType;
        if (f.F08BLevel is not null) draft.F08BLevel = f.F08BLevel;
    }
}
