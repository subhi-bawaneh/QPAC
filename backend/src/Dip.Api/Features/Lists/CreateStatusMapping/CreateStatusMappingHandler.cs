using Dip.Api.Features.Lists.GetStatusMappings;
using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.CreateStatusMapping;

public sealed class CreateStatusMappingHandler
    : ICommandHandler<CreateStatusMappingCommand, CreateStatusMappingResult>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CreateStatusMappingHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<CreateStatusMappingResult> Handle(
        CreateStatusMappingCommand command, CancellationToken ct)
    {
        var status = command.AconexStatus.Trim();
        var existing = await _db.StatusMappings.FirstOrDefaultAsync(
            m => m.ProjectId == command.ProjectId && m.AconexStatus.ToUpper() == status.ToUpper(), ct);

        if (existing is not null && !existing.IsDeleted)
        {
            throw new ConflictException($"'{status}' is already mapped");
        }

        if (existing is not null)
        {
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.Status = command.Status;
            existing.IsLegacy = command.IsLegacy;
            Audited.MarkEdited(existing, By, DateTime.UtcNow);
            Audited.Row(_db, existing.ProjectId, nameof(StatusMapping), existing.Id,
                Audited.Restore, null, existing.AconexStatus, By, DateTime.UtcNow);
            return new CreateStatusMappingResult(StatusMappingDto.From(existing), Restored: true);
        }

        var mapping = new StatusMapping
        {
            ProjectId = command.ProjectId,
            AconexStatus = status,
            Status = command.Status,
            IsLegacy = command.IsLegacy,
        };
        Audited.MarkEdited(mapping, By, DateTime.UtcNow);
        _db.StatusMappings.Add(mapping);
        Audited.Row(_db, mapping.ProjectId, nameof(StatusMapping), mapping.Id,
            Audited.Create, null, mapping.AconexStatus, By, DateTime.UtcNow);
        return new CreateStatusMappingResult(StatusMappingDto.From(mapping), Restored: false);
    }

    private string By => _currentUser.UserName ?? "system";
}
