using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.DeleteFile;

public sealed class DeleteFileHandler : ICommandHandler<DeleteFileCommand, Unit>
{
    private readonly DipDbContext _db;

    public DeleteFileHandler(DipDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteFileCommand command, CancellationToken ct)
    {
        var file = await _db.FolderFiles.FirstOrDefaultAsync(f => f.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"File {command.Id} not found");

        // Delete the local copy if present. Missing file is not an error - Drive sync may not have downloaded it.
        if (!string.IsNullOrEmpty(file.StoragePath) && System.IO.File.Exists(file.StoragePath))
        {
            try { System.IO.File.Delete(file.StoragePath); }
            catch (IOException) { /* leave orphan for the operator to clean up */ }
        }

        _db.FolderFiles.Remove(file);
        return Unit.Value;
    }
}
