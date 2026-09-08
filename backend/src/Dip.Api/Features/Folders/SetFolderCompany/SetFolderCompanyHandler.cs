using Dip.Application.Abstractions;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.SetFolderCompany;

public sealed class SetFolderCompanyHandler : ICommandHandler<SetFolderCompanyCommand, FolderNode>
{
    private readonly DipDbContext _db;

    public SetFolderCompanyHandler(DipDbContext db) => _db = db;

    public async Task<FolderNode> Handle(SetFolderCompanyCommand command, CancellationToken ct)
    {
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == command.Id && !f.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Folder {command.Id} not found");

        string? authorName = null;
        if (command.AuthorId is not null)
        {
            var author = await _db.PicklistItems.FirstOrDefaultAsync(
                p => p.Id == command.AuthorId.Value
                    && p.ProjectId == folder.ProjectId
                    && p.Field == PicklistField.Author
                    && !p.IsDeleted, ct)
                ?? throw new FluentValidation.ValidationException(
                    "The author must be a current item of the Author list");
            authorName = author.Code;
        }

        folder.IsCompany = command.IsCompany;
        folder.AuthorId = command.AuthorId;

        var childCount = await _db.Folders.CountAsync(f => f.ParentId == folder.Id && !f.IsDeleted, ct);
        var fileCount = await _db.FolderFiles.CountAsync(f => f.FolderId == folder.Id && !f.IsDeleted, ct);

        return new FolderNode(
            folder.Id, folder.ParentId, folder.Name, folder.Path, folder.Target,
            folder.DriveFolderId, folder.LastSyncedAt, folder.IsCompany, folder.AuthorId, authorName,
            childCount, fileCount, HasNewerDraft: false);
    }
}
