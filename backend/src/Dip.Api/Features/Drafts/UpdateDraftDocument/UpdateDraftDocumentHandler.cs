using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Drafts.UpdateDraftDocument;

public sealed class UpdateDraftDocumentHandler
    : ICommandHandler<UpdateDraftDocumentCommand, DraftDocumentDto>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateDraftDocumentHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DraftDocumentDto> Handle(UpdateDraftDocumentCommand command, CancellationToken ct)
    {
        var draft = await _db.DocumentDrafts
            .Include(d => d.Exchanges)
            .FirstOrDefaultAsync(d => d.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Draft document {command.Id} not found");

        // The AuthorizationBehavior only sees the *incoming* discipline; a scoped
        // editor must also be allowed to touch the row as it stands today.
        DraftScope.EnsureCanEdit(_currentUser, draft.F05Discipline);

        draft.Title = command.Title;
        draft.ExtractedFromModel = command.ExtractedFromModel;
        draft.ScopeArea = command.ScopeArea;
        draft.AuthoringSoftware = command.AuthoringSoftware;
        draft.ExchangeFormat = command.ExchangeFormat;
        draft.Scale = command.Scale;
        draft.DeliveryMilestone = command.DeliveryMilestone;
        draft.PackageName = command.PackageName;
        draft.ActivityId = command.ActivityId;
        draft.ClassificationCode = command.ClassificationCode;
        draft.F01Project = command.F01Project;
        draft.F02Originator = command.F02Originator;
        draft.F03Contract = command.F03Contract;
        draft.F04DocType = command.F04DocType;
        draft.F05Discipline = command.F05Discipline;
        draft.F06Zone = command.F06Zone;
        draft.F07Building = command.F07Building;
        draft.F08ADrawingType = command.F08ADrawingType;
        draft.F08BLevel = command.F08BLevel;
        draft.F08CSequence = command.F08CSequence;
        draft.CorporateDiscipline = command.CorporateDiscipline;

        if (command.Exchanges is not null)
        {
            ReplaceExchanges(draft, command.Exchanges);
            draft.BudgetWeight = draft.Exchanges.FirstOrDefault(e => e.Number == 1)?.DurationDays ?? 1m;
        }

        draft.UpdatedAt = DateTime.UtcNow;
        draft.UpdatedBy = _currentUser.UserName ?? "system";

        var previousNumber = DraftRecalculator.ApplyNumber(draft);
        await DraftRecalculator.RefreshAsync(
            _db, draft.ProjectId, draft.FolderFileId, new[] { draft }, new[] { previousNumber }, ct);

        return DraftDocumentDto.From(draft);
    }

    private void ReplaceExchanges(DocumentDraft draft, IReadOnlyList<DraftExchangeInput> inputs)
    {
        foreach (var existing in draft.Exchanges.ToList())
        {
            if (inputs.All(i => i.Number != existing.Number))
            {
                draft.Exchanges.Remove(existing);
                _db.DataExchangeDrafts.Remove(existing);
            }
        }

        foreach (var input in inputs)
        {
            var target = draft.Exchanges.FirstOrDefault(e => e.Number == input.Number);
            if (target is null)
            {
                target = new DataExchangeDraft { Number = input.Number };
                draft.Exchanges.Add(target);
            }

            target.Stage = input.Stage;
            target.ProgrammeRef = input.ProgrammeRef;
            target.Author = input.Author;
            target.Geometrical = input.Geometrical;
            target.NonGeometrical = input.NonGeometrical;
            target.DurationDays = input.DurationDays;
            target.Predecessor = input.Predecessor;
            target.ExchangeDate = input.ExchangeDate;
        }
    }
}

