using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.DeleteStatusMapping;

public sealed class DeleteStatusMappingHandler : ICommandHandler<DeleteStatusMappingCommand, Unit>
{
    private readonly DipDbContext _db;

    public DeleteStatusMappingHandler(DipDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteStatusMappingCommand command, CancellationToken ct)
    {
        var mapping = await _db.StatusMappings.FirstOrDefaultAsync(m => m.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Status mapping {command.Id} not found");

        mapping.IsDeleted = true;
        mapping.DeletedAt = DateTime.UtcNow;
        return Unit.Value;
    }
}
