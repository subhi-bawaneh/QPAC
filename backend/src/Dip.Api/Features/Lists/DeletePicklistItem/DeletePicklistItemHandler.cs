using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.DeletePicklistItem;

public sealed class DeletePicklistItemHandler : ICommandHandler<DeletePicklistItemCommand, Unit>
{
    private readonly DipDbContext _db;

    public DeletePicklistItemHandler(DipDbContext db) => _db = db;

    public async Task<Unit> Handle(DeletePicklistItemCommand command, CancellationToken ct)
    {
        var item = await _db.PicklistItems.FirstOrDefaultAsync(p => p.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Picklist item {command.Id} not found");

        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        return Unit.Value;
    }
}
