using Dip.Api.Workers;
using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Documents.UpdateDocument;

// Editing a Live row is the counterpart of editing a draft row: same fields, same
// derived number, but every changed field is written to AuditLog because there is no
// promote step to review it and no rollback to undo it (decision D7).
public sealed class UpdateDocumentHandler : ICommandHandler<UpdateDocumentCommand, DocumentDto>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly WorkQueue _queue;

    public UpdateDocumentHandler(DipDbContext db, ICurrentUser currentUser, WorkQueue queue)
    {
        _db = db;
        _currentUser = currentUser;
        _queue = queue;
    }

    public async Task<DocumentDto> Handle(UpdateDocumentCommand command, CancellationToken ct)
    {
        var document = await _db.Documents
            .Include(d => d.Exchanges)
            .FirstOrDefaultAsync(d => d.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Document {command.Id} not found");

        // The AuthorizationBehavior only sees the incoming discipline; a scoped editor
        // must also be allowed to touch the row as it stands today.

        var before = DocumentDiff.From(document);
        var beforeNumber = document.DocumentNumber;
        var beforeFields = NumberingFields(document);

        document.Title = command.Title;
        document.ExtractedFromModel = command.ExtractedFromModel;
        document.ScopeArea = command.ScopeArea;
        document.AuthoringSoftware = command.AuthoringSoftware;
        document.ExchangeFormat = command.ExchangeFormat;
        document.Scale = command.Scale;
        document.DeliveryMilestone = command.DeliveryMilestone;
        document.PackageName = command.PackageName;
        document.ActivityId = command.ActivityId;
        document.ClassificationCode = command.ClassificationCode;
        document.F01Project = command.F01Project;
        document.F02Originator = command.F02Originator;
        document.F03Contract = command.F03Contract;
        document.F04DocType = command.F04DocType;
        document.F05Discipline = command.F05Discipline;
        // The width is data: an editor who retypes a field gets the number recomposed
        // with the width configured for the type, not a hard-coded four.
        var widths = SerialWidths.Create(
            await _db.DocumentTypeSerials
                .Where(s => s.ProjectId == document.ProjectId && !s.IsDeleted)
                .ToListAsync(ct));
        var sequenceWidth = widths.For(command.F04DocType);

        document.F06Zone = DocumentNumbering.NormalizeZone(command.F06Zone);
        document.F07Building = command.F07Building;
        document.F08ADrawingType = command.F08ADrawingType;
        document.F08BLevel = command.F08BLevel;
        document.F08CSequence = DocumentNumbering.NormalizeSequence(command.F08CSequence, sequenceWidth);
        document.CorporateDiscipline = command.CorporateDiscipline;

        document.DocumentNumber = DocumentNumbering.Compose(
            document.F01Project, document.F02Originator, document.F03Contract, document.F04DocType,
            document.F05Discipline, document.F06Zone, document.F07Building,
            document.F08ADrawingType, document.F08BLevel, document.F08CSequence, sequenceWidth);

        if (!string.Equals(document.DocumentNumber, beforeNumber, StringComparison.OrdinalIgnoreCase))
        {
            var taken = await _db.Documents.AnyAsync(
                d => d.ProjectId == document.ProjectId
                    && d.Id != document.Id
                    && d.DocumentNumber.ToUpper() == document.DocumentNumber.ToUpper(), ct);
            if (taken)
            {
                throw new ConflictException(
                    $"Document number '{document.DocumentNumber}' already exists in this project");
            }
        }

        if (command.Exchanges is not null)
        {
            ReplaceExchanges(document, command.Exchanges);
            document.BudgetWeight = document.Exchanges.FirstOrDefault(e => e.Number == 1)?.DurationDays ?? 1m;
        }

        var by = _currentUser.UserName ?? "system";
        var at = DateTime.UtcNow;
        document.UpdatedAt = at;
        document.UpdatedBy = by;

        // DocumentDiff deliberately leaves the eight numbering fields out — a change there
        // makes a *different* document in the draft workflow. On a Live edit it is the
        // same row being renumbered, so those changes are audited too.
        var changes = DocumentDiff.Changes(before, DocumentDiff.From(document))
            .Concat(NumberingChanges(beforeFields, NumberingFields(document)))
            .ToList();

        if (changes.Count > 0)
        {
            Audited.MarkEdited(document, by, at);
            Audited.Changes(_db, document.ProjectId, nameof(Document), document.Id, changes, by, at);
        }

        // The row's computed columns are stale until the engine runs again.
        var stale = await _db.DocumentSnapshots
            .Where(s => s.DocumentId == document.Id)
            .ToListAsync(ct);
        _db.DocumentSnapshots.RemoveRange(stale);

        var projectId = document.ProjectId;
        _db.SavedChanges += Enqueue;

        void Enqueue(object? sender, SavedChangesEventArgs args)
        {
            _db.SavedChanges -= Enqueue;
            _queue.EnqueueRecalculate(projectId);
        }

        return DocumentDto.From(document);
    }

    private static IReadOnlyList<(string Field, string Value)> NumberingFields(Document d) =>
    [
        (nameof(Document.DocumentNumber), d.DocumentNumber),
        (nameof(Document.F01Project), d.F01Project),
        (nameof(Document.F02Originator), d.F02Originator),
        (nameof(Document.F03Contract), d.F03Contract),
        (nameof(Document.F04DocType), d.F04DocType),
        (nameof(Document.F05Discipline), d.F05Discipline),
        (nameof(Document.F06Zone), d.F06Zone),
        (nameof(Document.F07Building), d.F07Building),
        (nameof(Document.F08ADrawingType), d.F08ADrawingType),
        (nameof(Document.F08BLevel), d.F08BLevel),
        (nameof(Document.F08CSequence), d.F08CSequence),
    ];

    private static IEnumerable<FieldChange> NumberingChanges(
        IReadOnlyList<(string Field, string Value)> before,
        IReadOnlyList<(string Field, string Value)> after)
    {
        for (var i = 0; i < before.Count; i++)
        {
            if (!string.Equals(before[i].Value, after[i].Value, StringComparison.Ordinal))
            {
                yield return new FieldChange(before[i].Field, before[i].Value, after[i].Value);
            }
        }
    }

    private void ReplaceExchanges(Document document, IReadOnlyList<ExchangeInput> inputs)
    {
        foreach (var existing in document.Exchanges.ToList())
        {
            if (inputs.All(i => i.Number != existing.Number))
            {
                document.Exchanges.Remove(existing);
                _db.DataExchanges.Remove(existing);
            }
        }

        foreach (var input in inputs)
        {
            var target = document.Exchanges.FirstOrDefault(e => e.Number == input.Number);
            if (target is null)
            {
                target = new DataExchange { Number = input.Number };
                document.Exchanges.Add(target);
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
