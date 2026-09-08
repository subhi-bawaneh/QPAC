using Dip.Api.Features.Lists.GetPicklists;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.RestorePicklistItem;

public sealed class RestorePicklistItemHandler
    : ICommandHandler<RestorePicklistItemCommand, PicklistItemDto>
{
    private readonly DipDbContext _db;

    public RestorePicklistItemHandler(DipDbContext db) => _db = db;

    public async Task<PicklistItemDto> Handle(RestorePicklistItemCommand command, CancellationToken ct)
    {
        var item = await _db.PicklistItems.FirstOrDefaultAsync(p => p.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Picklist item {command.Id} not found");

        // Someone may have created the code again while this row was deleted.
        var taken = await _db.PicklistItems.AnyAsync(
            p => p.ProjectId == item.ProjectId
                && p.Field == item.Field
                && p.Id != item.Id
                && !p.IsDeleted
                && p.Code.ToUpper() == item.Code.ToUpper(), ct);
        if (taken)
        {
            throw new ConflictException($"'{item.Code}' is already in the {item.Field} list");
        }

        item.IsDeleted = false;
        item.DeletedAt = null;
        return PicklistItemDto.From(item);
    }
}
