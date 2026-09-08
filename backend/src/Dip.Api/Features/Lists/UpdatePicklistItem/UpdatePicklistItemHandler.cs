using Dip.Api.Features.Lists.GetPicklists;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.UpdatePicklistItem;

public sealed class UpdatePicklistItemHandler
    : ICommandHandler<UpdatePicklistItemCommand, PicklistItemDto>
{
    private readonly DipDbContext _db;

    public UpdatePicklistItemHandler(DipDbContext db) => _db = db;

    public async Task<PicklistItemDto> Handle(UpdatePicklistItemCommand command, CancellationToken ct)
    {
        var item = await _db.PicklistItems.FirstOrDefaultAsync(p => p.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Picklist item {command.Id} not found");

        var code = command.Code.Trim();
        var collides = await _db.PicklistItems.AnyAsync(
            p => p.ProjectId == item.ProjectId
                && p.Field == item.Field
                && p.Id != item.Id
                && !p.IsDeleted
                && p.Code.ToUpper() == code.ToUpper(), ct);
        if (collides)
        {
            throw new ConflictException($"'{code}' is already in the {item.Field} list");
        }

        item.Code = code;
        item.Description = command.Description.Trim();
        item.SortOrder = command.SortOrder;
        return PicklistItemDto.From(item);
    }
}
