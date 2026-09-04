using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.CreateFolder;

public sealed class CreateFolderHandler : ICommandHandler<CreateFolderCommand, Guid>
{
    private readonly DipDbContext _db;

    public CreateFolderHandler(DipDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateFolderCommand command, CancellationToken ct)
    {
        Folder? parent = null;
        if (command.ParentId is not null)
        {
            parent = await _db.Folders
                .FirstOrDefaultAsync(f => f.Id == command.ParentId.Value && !f.IsDeleted, ct)
                ?? throw new KeyNotFoundException($"Parent folder {command.ParentId} not found");

            if (parent.ProjectId != command.ProjectId)
            {
                throw new FluentValidation.ValidationException("Parent folder belongs to a different project");
            }
        }

        var path = parent is null ? command.Name : $"{parent.Path}/{command.Name}";

        var existing = await _db.Folders
            .AnyAsync(f => f.ProjectId == command.ProjectId && f.Path == path && !f.IsDeleted, ct);
        if (existing)
        {
            throw new FluentValidation.ValidationException($"Folder '{path}' already exists");
        }

        var folder = new Folder
        {
            ProjectId = command.ProjectId,
            ParentId = command.ParentId,
            Name = command.Name,
            Path = path,
            Target = parent?.Target ?? Domain.Enums.DataTarget.Live,
            SortOrder = 0,
        };

        _db.Folders.Add(folder);
        return folder.Id;
    }
}
