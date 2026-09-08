using Dip.Api.Features.Lists.GetStatusMappings;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.RestoreStatusMapping;

public sealed class RestoreStatusMappingHandler
    : ICommandHandler<RestoreStatusMappingCommand, StatusMappingDto>
{
    private readonly DipDbContext _db;

    public RestoreStatusMappingHandler(DipDbContext db) => _db = db;

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

        mapping.IsDeleted = false;
        mapping.DeletedAt = null;
        return StatusMappingDto.From(mapping);
    }
}
