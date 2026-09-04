using Dip.Application.Abstractions;
using Dip.Application.Files;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.UploadFile;

public sealed class UploadFileHandler : ICommandHandler<UploadFileCommand, Guid>
{
    private readonly DipDbContext _db;
    private readonly ILocalFileStorage _storage;

    public UploadFileHandler(DipDbContext db, ILocalFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<Guid> Handle(UploadFileCommand command, CancellationToken ct)
    {
        var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == command.FolderId && !f.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Folder {command.FolderId} not found");

        var file = new FolderFile
        {
            FolderId = folder.Id,
            Name = command.FileName,
            Kind = FileKindDetector.Detect(command.FileName),
            Source = FileSource.Upload,
            State = ImportState.NotImported,
            SizeBytes = command.SizeBytes,
        };
        _db.FolderFiles.Add(file);

        // Persist to local storage using the entity id so the filename is stable.
        var stored = await _storage.SaveAsync(file.Id, command.FileName, command.Content, ct);
        file.StoragePath = stored.StoragePath;
        file.SizeBytes = stored.SizeBytes;
        file.Md5 = stored.Md5;

        return file.Id;
    }
}
