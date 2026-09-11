using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Tidps.ListTidpFiles;

public sealed class ListTidpFilesHandler
    : IQueryHandler<ListTidpFilesQuery, IReadOnlyCollection<TidpFileDto>>
{
    private readonly DipDbContext _db;

    public ListTidpFilesHandler(DipDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<TidpFileDto>> Handle(
        ListTidpFilesQuery query, CancellationToken ct)
    {
        // DocumentCount and EditedCount are counted in SQL rather than by loading rows:
        // a file holds hundreds of documents and the explorer only shows the totals.
        return await _db.TidpFiles
            .AsNoTracking()
            .Where(t => t.ProjectId == query.ProjectId)
            .OrderBy(t => t.Discipline!.CorporateName)
            .ThenBy(t => t.FileName)
            .Select(t => new TidpFileDto(
                t.Id,
                t.ProjectId,
                t.DisciplineId,
                t.Discipline!.Code,
                t.Discipline.CorporateName,
                t.FileName,
                t.DocumentReference,
                t.RevisionNumber,
                t.RowsRead,
                t.RowsImported,
                t.RowsSkipped,
                _db.Documents.Count(d => d.TidpFileId == t.Id),
                _db.Documents.Count(d => d.TidpFileId == t.Id && d.IsEdited),
                t.UploadedBy,
                t.UploadedAt,
                t.Status,
                t.Error))
            .ToListAsync(ct);
    }
}
