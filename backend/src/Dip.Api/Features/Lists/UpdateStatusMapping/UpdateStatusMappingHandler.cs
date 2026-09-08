using Dip.Api.Features.Lists.GetStatusMappings;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.UpdateStatusMapping;

public sealed class UpdateStatusMappingHandler
    : ICommandHandler<UpdateStatusMappingCommand, StatusMappingDto>
{
    private readonly DipDbContext _db;

    public UpdateStatusMappingHandler(DipDbContext db) => _db = db;

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

        mapping.AconexStatus = status;
        mapping.Status = command.Status;
        mapping.IsLegacy = command.IsLegacy;
        return StatusMappingDto.From(mapping);
    }
}
