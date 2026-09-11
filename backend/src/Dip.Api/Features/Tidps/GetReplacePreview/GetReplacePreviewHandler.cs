using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Tidps.GetReplacePreview;

public sealed class GetReplacePreviewHandler : IQueryHandler<GetReplacePreviewQuery, ReplacePreview>
{
    private readonly DipDbContext _db;

    public GetReplacePreviewHandler(DipDbContext db) => _db = db;

    public async Task<ReplacePreview> Handle(GetReplacePreviewQuery query, CancellationToken ct)
    {
        var file = await _db.TidpFiles.AsNoTracking()
            .Include(t => t.Discipline)
            .FirstOrDefaultAsync(t => t.Id == query.TidpFileId, ct)
            ?? throw new KeyNotFoundException($"TIDP file {query.TidpFileId} not found");

        var rows = await _db.Documents.CountAsync(d => d.TidpFileId == file.Id, ct);
        var edited = await _db.Documents.CountAsync(d => d.TidpFileId == file.Id && d.IsEdited, ct);

        return new ReplacePreview(
            file.Id, file.FileName, file.Discipline?.CorporateName ?? string.Empty,
            rows, edited, file.UploadedAt, file.UploadedBy);
    }
}
