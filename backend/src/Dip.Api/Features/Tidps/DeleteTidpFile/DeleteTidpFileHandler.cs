using Dip.Api.Features.Audit;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Tidps.DeleteTidpFile;

public sealed class DeleteTidpFileHandler : ICommandHandler<DeleteTidpFileCommand, DeleteResult>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _user;

    public DeleteTidpFileHandler(DipDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<DeleteResult> Handle(DeleteTidpFileCommand command, CancellationToken ct)
    {
        var file = await _db.TidpFiles.FirstOrDefaultAsync(t => t.Id == command.TidpFileId, ct)
            ?? throw new KeyNotFoundException($"TIDP file {command.TidpFileId} not found");

        var by = _user.UserName ?? "system";
        var dumped = await AuditDump.DocumentsAsync(
            _db, file.ProjectId, file.Id, AuditDump.Deleted, by, ct);
        await _db.SaveChangesAsync(ct);

        // Snapshots cascade from Documents, and Documents cascade from TidpFiles, so
        // removing the file row takes the whole register entry with it.
        var removed = await _db.Documents.CountAsync(d => d.TidpFileId == file.Id, ct);
        _db.TidpFiles.Remove(file);

        return new DeleteResult(file.Id, removed, dumped);
    }
}
