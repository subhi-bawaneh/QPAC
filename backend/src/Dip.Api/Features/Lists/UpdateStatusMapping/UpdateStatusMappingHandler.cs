using Dip.Domain.Entities;
using Dip.Api.Features.Lists.GetStatusMappings;
using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.UpdateStatusMapping;

public sealed class UpdateStatusMappingHandler
    : ICommandHandler<UpdateStatusMappingCommand, StatusMappingDto>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public UpdateStatusMappingHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StatusMappingDto> Handle(UpdateStatusMappingCommand command, CancellationToken ct)
    {
        var mapping = await _db.StatusMappings.FirstOrDefaultAsync(m => m.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Status mapping {command.Id} not found");

        var status = command.AconexStatus.Trim();
        var collides = await _db.StatusMappings.AnyAsync(
            m => m.ProjectId == mapping.ProjectId
                && m.Id != mapping.Id
                && !m.IsDeleted
                && m.AconexStatus.ToUpper() == status.ToUpper(), ct);
        if (collides)
        {
            throw new ConflictException($"'{status}' is already mapped");
        }

        var by = _currentUser.UserName ?? "system";
        var at = DateTime.UtcNow;
        var changes = new List<Dip.Application.Documents.FieldChange>();
        if (mapping.AconexStatus != status)
        {
            changes.Add(new(nameof(mapping.AconexStatus), mapping.AconexStatus, status));
        }
        if (mapping.Status != command.Status)
        {
            changes.Add(new(nameof(mapping.Status), mapping.Status.ToString(), command.Status.ToString()));
        }
        if (mapping.IsLegacy != command.IsLegacy)
        {
            changes.Add(new(nameof(mapping.IsLegacy),
                mapping.IsLegacy.ToString(), command.IsLegacy.ToString()));
        }

        mapping.AconexStatus = status;
        mapping.Status = command.Status;
        mapping.IsLegacy = command.IsLegacy;
        Audited.MarkEdited(mapping, by, at);
        Audited.Changes(_db, mapping.ProjectId, nameof(StatusMapping), mapping.Id, changes, by, at);
        return StatusMappingDto.From(mapping);
    }
}
