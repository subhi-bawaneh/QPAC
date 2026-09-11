using Dip.Application.Abstractions;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Aconex.PreviewAconexUpload;

public sealed class PreviewAconexUploadHandler
    : ICommandHandler<PreviewAconexUploadCommand, AconexPreview>
{
    private readonly DipDbContext _db;
    private readonly AconexHistoryImporter _importer;

    public PreviewAconexUploadHandler(DipDbContext db, AconexHistoryImporter importer)
    {
        _db = db;
        _importer = importer;
    }

    public async Task<AconexPreview> Handle(PreviewAconexUploadCommand command, CancellationToken ct)
    {
        var held = (await _db.AconexRevisions
                .Where(a => a.ProjectId == command.ProjectId)
                .Select(a => a.LineHash)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var files = new List<AconexFilePreview>(command.Files.Count);
        foreach (var file in command.Files)
        {
            try
            {
                // Counted against the files already previewed in this same request too:
                // uploading the same export twice in one go must not read as 2 x new.
                var hashes = _importer.ReadLineHashes(
                    new MemoryStream(file.Content, writable: false));

                var fresh = 0;
                var duplicate = 0;
                foreach (var hash in hashes)
                {
                    if (held.Add(hash)) fresh++;
                    else duplicate++;
                }

                files.Add(new AconexFilePreview(file.FileName, hashes.Count, fresh, duplicate, null));
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException)
            {
                files.Add(new AconexFilePreview(file.FileName, 0, 0, 0, ex.Message));
            }
        }

        return new AconexPreview(
            files,
            files.Sum(f => f.RowsRead),
            files.Sum(f => f.RowsNew),
            files.Sum(f => f.RowsDuplicate));
    }
}
