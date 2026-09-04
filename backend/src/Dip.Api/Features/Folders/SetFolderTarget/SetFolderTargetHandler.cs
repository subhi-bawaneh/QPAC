using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.SetFolderTarget;

public sealed class SetFolderTargetHandler : ICommandHandler<SetFolderTargetCommand, Unit>
{
    private readonly DipDbContext _db;

    public SetFolderTargetHandler(DipDbContext db) => _db = db;

    public async Task<Unit> Handle(SetFolderTargetCommand command, CancellationToken ct)
    {
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == command.Id && !f.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Folder {command.Id} not found");

        folder.Target = command.Target;
        return Unit.Value;
    }
}
