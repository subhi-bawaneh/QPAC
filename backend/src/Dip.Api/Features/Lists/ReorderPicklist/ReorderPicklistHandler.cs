using Dip.Domain.Entities;
using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.ReorderPicklist;

public sealed class ReorderPicklistHandler : ICommandHandler<ReorderPicklistCommand, Unit>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ReorderPicklistHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(ReorderPicklistCommand command, CancellationToken ct)
    {
        var items = await _db.PicklistItems
            .Where(p => p.ProjectId == command.ProjectId && p.Field == command.Field && !p.IsDeleted)
            .ToListAsync(ct);
        var byId = items.ToDictionary(p => p.Id);

        var by = _currentUser.UserName ?? "system";
        var at = DateTime.UtcNow;
        var before = items.ToDictionary(p => p.Id, p => p.SortOrder);

        var order = 0;
        foreach (var id in command.Ids)
        {
            if (!byId.TryGetValue(id, out var item))
            {
                throw new FluentValidation.ValidationException(
                    $"Item {id} is not a current member of the {command.Field} list");
            }
            item.SortOrder = ++order;
        }

        // Anything the caller did not name keeps a stable place after the named rows.
        foreach (var item in items.Where(p => !command.Ids.Contains(p.Id)).OrderBy(p => p.SortOrder))
        {
            item.SortOrder = ++order;
        }

        // One audit row per row that actually moved: reordering a list of forty to put
        // one code at the top must not read as forty separate decisions.
        foreach (var item in items.Where(p => before[p.Id] != p.SortOrder))
        {
            Audited.MarkEdited(item, by, at);
            Audited.Changes(_db, item.ProjectId, nameof(PicklistItem), item.Id,
                new[]
                {
                    new Dip.Application.Documents.FieldChange(
                        nameof(item.SortOrder),
                        before[item.Id].ToString(System.Globalization.CultureInfo.InvariantCulture),
                        item.SortOrder.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                },
                by, at);
        }

        return Unit.Value;
    }
}
