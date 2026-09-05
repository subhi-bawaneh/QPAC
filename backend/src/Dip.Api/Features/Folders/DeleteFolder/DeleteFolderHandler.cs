using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.DeleteFolder;

public sealed class DeleteFolderHandler : ICommandHandler<DeleteFolderCommand, Unit>
{
    private readonly DipDbContext _db;

    public DeleteFolderHandler(DipDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteFolderCommand command, CancellationToken ct)
    {
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == command.Id && !f.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Folder {command.Id} not found");

        var children = await _db.Folders.CountAsync(f => f.ParentId == folder.Id && !f.IsDeleted, ct);
        if (children > 0)
        {
            throw new FluentValidation.ValidationException(
                $"'{folder.Name}' still has {children} subfolder(s). Delete those first.");
        }

        var files = await _db.FolderFiles.CountAsync(f => f.FolderId == folder.Id, ct);
        if (files > 0)
        {
            throw new FluentValidation.ValidationException(
                $"'{folder.Name}' still holds {files} file(s). Delete those first.");
        }

        // Soft delete: imported rows reference the folder's files by id, and a hard
        // delete would strand that history.
        folder.IsDeleted = true;
        return Unit.Value;
    }
}
