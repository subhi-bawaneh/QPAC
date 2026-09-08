using Dip.Api.Features.Lists.GetPicklists;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.CreatePicklistItem;

public sealed class CreatePicklistItemHandler
    : ICommandHandler<CreatePicklistItemCommand, CreatePicklistItemResult>
{
    private readonly DipDbContext _db;

    public CreatePicklistItemHandler(DipDbContext db) => _db = db;

    public async Task<CreatePicklistItemResult> Handle(
        CreatePicklistItemCommand command, CancellationToken ct)
    {
        var code = command.Code.Trim();
        var existing = await _db.PicklistItems.FirstOrDefaultAsync(
            p => p.ProjectId == command.ProjectId
                && p.Field == command.Field
                && p.Code.ToUpper() == code.ToUpper(), ct);

        if (existing is not null && !existing.IsDeleted)
        {
            throw new ConflictException($"'{code}' is already in the {command.Field} list");
        }

        var sortOrder = command.SortOrder ?? await NextSortOrderAsync(command, ct);

        if (existing is not null)
        {
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.Description = command.Description.Trim();
            existing.SortOrder = sortOrder;
            return new CreatePicklistItemResult(PicklistItemDto.From(existing), Restored: true);
        }

        var item = new PicklistItem
        {
            ProjectId = command.ProjectId,
            Field = command.Field,
            Code = code,
            Description = command.Description.Trim(),
            SortOrder = sortOrder,
        };
        _db.PicklistItems.Add(item);
        return new CreatePicklistItemResult(PicklistItemDto.From(item), Restored: false);
    }

    private async Task<int> NextSortOrderAsync(CreatePicklistItemCommand command, CancellationToken ct)
    {
        var max = await _db.PicklistItems
            .Where(p => p.ProjectId == command.ProjectId && p.Field == command.Field && !p.IsDeleted)
            .Select(p => (int?)p.SortOrder)
            .MaxAsync(ct);
        return (max ?? 0) + 1;
    }
}
