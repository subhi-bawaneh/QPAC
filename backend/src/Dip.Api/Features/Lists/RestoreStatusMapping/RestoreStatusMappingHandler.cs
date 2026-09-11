using Dip.Domain.Entities;
using Dip.Api.Features.Lists.GetStatusMappings;
using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.RestoreStatusMapping;

public sealed class RestoreStatusMappingHandler
    : ICommandHandler<RestoreStatusMappingCommand, StatusMappingDto>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public RestoreStatusMappingHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StatusMappingDto> Handle(RestoreStatusMappingCommand command, CancellationToken ct)
    {
        var mapping = await _db.StatusMappings.FirstOrDefaultAsync(m => m.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Status mapping {command.Id} not found");

        var taken = await _db.StatusMappings.AnyAsync(
            m => m.ProjectId == mapping.ProjectId
                && m.Id != mapping.Id
                && !m.IsDeleted
                && m.AconexStatus.ToUpper() == mapping.AconexStatus.ToUpper(), ct);
        if (taken)
        {
            throw new ConflictException($"'{mapping.AconexStatus}' is already mapped");
        }

        var by = _currentUser.UserName ?? "system";
        var at = DateTime.UtcNow;
        mapping.IsDeleted = false;
        mapping.DeletedAt = null;
        Audited.MarkEdited(mapping, by, at);
        Audited.Row(_db, mapping.ProjectId, nameof(StatusMapping), mapping.Id,
            Audited.Restore, null, mapping.AconexStatus, by, at);
        return StatusMappingDto.From(mapping);
    }
}
