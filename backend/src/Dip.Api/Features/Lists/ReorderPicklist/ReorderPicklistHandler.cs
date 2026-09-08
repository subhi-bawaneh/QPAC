using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.ReorderPicklist;

public sealed class ReorderPicklistHandler : ICommandHandler<ReorderPicklistCommand, Unit>
{
    private readonly DipDbContext _db;

    public ReorderPicklistHandler(DipDbContext db) => _db = db;

    public async Task<Unit> Handle(ReorderPicklistCommand command, CancellationToken ct)
    {
        var items = await _db.PicklistItems
            .Where(p => p.ProjectId == command.ProjectId && p.Field == command.Field && !p.IsDeleted)
            .ToListAsync(ct);
        var byId = items.ToDictionary(p => p.Id);

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

        return Unit.Value;
    }
}
