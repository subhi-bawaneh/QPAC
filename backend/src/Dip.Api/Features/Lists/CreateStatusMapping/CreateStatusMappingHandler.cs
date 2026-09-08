using Dip.Api.Features.Lists.GetStatusMappings;
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

    public CreateStatusMappingHandler(DipDbContext db) => _db = db;

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
            return new CreateStatusMappingResult(StatusMappingDto.From(existing), Restored: true);
        }

        var mapping = new StatusMapping
        {
            ProjectId = command.ProjectId,
            AconexStatus = status,
            Status = command.Status,
            IsLegacy = command.IsLegacy,
        };
        _db.StatusMappings.Add(mapping);
        return new CreateStatusMappingResult(StatusMappingDto.From(mapping), Restored: false);
    }
}
