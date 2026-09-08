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

        // A company folder names its author through this row, so removing it would
        // leave that folder pointing at nothing.
        var inUse = await _db.Folders.AnyAsync(f => f.AuthorId == item.Id && !f.IsDeleted, ct);
        if (inUse)
        {
            throw new ConflictException(
                $"'{item.Code}' is the author of a company folder and cannot be deleted");
        }

        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        return Unit.Value;
    }
}
