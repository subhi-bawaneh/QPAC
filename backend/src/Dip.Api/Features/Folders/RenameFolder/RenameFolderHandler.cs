using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.RenameFolder;

public sealed class RenameFolderHandler : ICommandHandler<RenameFolderCommand, Unit>
{
    private readonly DipDbContext _db;

    public RenameFolderHandler(DipDbContext db) => _db = db;

    public async Task<Unit> Handle(RenameFolderCommand command, CancellationToken ct)
    {
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == command.Id && !f.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Folder {command.Id} not found");

        if (folder.Name == command.NewName)
        {
            return Unit.Value;
        }

        var oldPath = folder.Path;
        var newPath = folder.ParentId is null
            ? command.NewName
            : $"{oldPath[..oldPath.LastIndexOf('/')]}/{command.NewName}";

        var conflict = await _db.Folders
            .AnyAsync(f => f.ProjectId == folder.ProjectId && f.Path == newPath && f.Id != folder.Id && !f.IsDeleted, ct);
        if (conflict)
        {
            throw new FluentValidation.ValidationException($"Folder '{newPath}' already exists");
        }

        folder.Name = command.NewName;
        folder.Path = newPath;

        // Cascade path rename to descendants (path starts with old path + "/").
        var descendants = await _db.Folders
            .Where(f => f.ProjectId == folder.ProjectId && f.Path.StartsWith(oldPath + "/") && !f.IsDeleted)
            .ToListAsync(ct);
        foreach (var d in descendants)
        {
            d.Path = newPath + d.Path[oldPath.Length..];
        }

        return Unit.Value;
    }
}
