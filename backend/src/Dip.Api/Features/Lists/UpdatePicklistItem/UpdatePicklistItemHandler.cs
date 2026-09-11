using Dip.Domain.Entities;
using Dip.Api.Features.Lists.GetPicklists;
using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.UpdatePicklistItem;

public sealed class UpdatePicklistItemHandler
    : ICommandHandler<UpdatePicklistItemCommand, PicklistItemDto>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdatePicklistItemHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

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

        var by = _currentUser.UserName ?? "system";
        var at = DateTime.UtcNow;
        var changes = new List<Dip.Application.Documents.FieldChange>();
        if (item.Code != code) changes.Add(new(nameof(item.Code), item.Code, code));
        if (item.Description != command.Description.Trim())
        {
            changes.Add(new(nameof(item.Description), item.Description, command.Description.Trim()));
        }
        if (item.SortOrder != command.SortOrder)
        {
            changes.Add(new(nameof(item.SortOrder),
                item.SortOrder.ToString(System.Globalization.CultureInfo.InvariantCulture),
                command.SortOrder.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        item.Code = code;
        item.Description = command.Description.Trim();
        item.SortOrder = command.SortOrder;
        Audited.MarkEdited(item, by, at);
        Audited.Changes(_db, item.ProjectId, nameof(PicklistItem), item.Id, changes, by, at);
        return PicklistItemDto.From(item);
    }
}
