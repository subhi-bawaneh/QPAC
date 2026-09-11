using Dip.Domain.Entities;
using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Lists.DeleteStatusMapping;

public sealed class DeleteStatusMappingHandler : ICommandHandler<DeleteStatusMappingCommand, Unit>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DeleteStatusMappingHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteStatusMappingCommand command, CancellationToken ct)
    {
        var mapping = await _db.StatusMappings.FirstOrDefaultAsync(m => m.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Status mapping {command.Id} not found");

        var by = _currentUser.UserName ?? "system";
        var at = DateTime.UtcNow;
        mapping.IsDeleted = true;
        mapping.DeletedAt = at;
        Audited.MarkEdited(mapping, by, at);
        Audited.Row(_db, mapping.ProjectId, nameof(StatusMapping), mapping.Id,
            Audited.Delete, mapping.AconexStatus, null, by, at);
        return Unit.Value;
    }
}
