using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.DownloadFile;

public sealed class DownloadFileHandler : IQueryHandler<DownloadFileQuery, FileDownload>
{
    private readonly DipDbContext _db;

    public DownloadFileHandler(DipDbContext db) => _db = db;

    public async Task<FileDownload> Handle(DownloadFileQuery query, CancellationToken ct)
    {
        var file = await _db.FolderFiles.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == query.FileId && !f.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"File {query.FileId} not found");

        var blob = await _db.FileBlobs.AsNoTracking()
            .FirstOrDefaultAsync(b => b.FolderFileId == file.Id, ct)
            ?? throw new KeyNotFoundException($"File {query.FileId} has no stored content");

        return new FileDownload(file.Name, blob.Content);
    }
}
