using Dip.Domain.Entities;
using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.DeletePicklistItem;

public sealed class DeletePicklistItemHandler : ICommandHandler<DeletePicklistItemCommand, Unit>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DeletePicklistItemHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeletePicklistItemCommand command, CancellationToken ct)
    {
        var item = await _db.PicklistItems.FirstOrDefaultAsync(p => p.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Picklist item {command.Id} not found");

        var by = _currentUser.UserName ?? "system";
        var at = DateTime.UtcNow;
        item.IsDeleted = true;
        item.DeletedAt = at;
        Audited.MarkEdited(item, by, at);
        Audited.Row(_db, item.ProjectId, nameof(PicklistItem), item.Id,
            Audited.Delete, item.Code, null, by, at);
        return Unit.Value;
    }
}
