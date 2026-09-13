using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Tidps.GetTidpFolder;

public sealed class GetTidpFolderHandler : IQueryHandler<GetTidpFolderQuery, TidpFolderDto>
{
    private readonly DipDbContext _db;

    public GetTidpFolderHandler(DipDbContext db) => _db = db;

    public async Task<TidpFolderDto> Handle(GetTidpFolderQuery query, CancellationToken ct)
    {
        var owners = await _db.TidpFolderOwners.AsNoTracking()
            .Where(o => o.ProjectId == query.ProjectId)
            .ToListAsync(ct);

        var disciplines = await _db.TidpFolderDisciplines.AsNoTracking()
            .Where(d => d.ProjectId == query.ProjectId)
            .ToListAsync(ct);

        // Only folder-synced files: a workbook uploaded one at a time has no path and
        // so belongs to no folder. Document counts come from the database rather than
        // from loading rows — one file holds hundreds and none of them are shown here.
        var files = await _db.TidpFiles.AsNoTracking()
            .Where(t => t.ProjectId == query.ProjectId && t.RelativePath != null)
            .Select(t => Map(t, _db.Documents.Count(d => d.TidpFileId == t.Id), t.OwnerId, t.FolderDisciplineId))
            .ToListAsync(ct);

        var byDiscipline = files
            .Where(f => f.FolderDisciplineId is not null)
            .ToLookup(f => f.FolderDisciplineId!.Value);
        var looseByOwner = files
            .Where(f => f.FolderDisciplineId is null && f.OwnerId is not null)
            .ToLookup(f => f.OwnerId!.Value);
        var disciplinesByOwner = disciplines.ToLookup(d => d.OwnerId);

        var tree = owners
            // The folder's own order: `01.NAP` before `12. SANA AL-JAZERAH`, and any
            // folder without the numeric prefix after every folder that has one.
            .OrderBy(o => o.SortOrder ?? int.MaxValue)
            .ThenBy(o => o.FolderName, StringComparer.OrdinalIgnoreCase)
            .Select(o => new TidpOwnerDto(
                o.Id, o.RelativePath, o.FolderName, o.SortOrder, o.OwnerName, o.OwnerType,
                o.FolderStatus, o.MissingSince,
                disciplinesByOwner[o.Id]
                    .OrderBy(d => d.DisciplineCode, StringComparer.OrdinalIgnoreCase)
                    .Select(d => new TidpDisciplineFolderDto(
                        d.Id, d.RelativePath, d.FolderName, d.DisciplineCode, d.DisciplineName,
                        d.FolderStatus, d.MissingSince,
                        Sort(byDiscipline[d.Id])))
                    .ToList(),
                Sort(looseByOwner[o.Id])))
            .ToList();

        return new TidpFolderDto(
            query.ProjectId,
            owners.Count,
            files.Count,
            files.Count(f => f.FolderStatus == TidpFolderStatus.Missing),
            tree);
    }

    private static List<TidpFolderFileDto> Sort(IEnumerable<TidpFolderFileDto> files) =>
        files.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase).ToList();

    private static TidpFolderFileDto Map(TidpFile t, int documentCount, Guid? ownerId, Guid? disciplineId) => new(
        t.Id, t.RelativePath!, t.FileName, t.DisciplineTag, t.Sequence, t.NameZone, t.NameLevel,
        t.SizeBytes, t.LastModifiedUtc, t.LastSeenAt, t.ContentHash,
        t.FolderStatus, t.MissingSince, t.Status, t.Error,
        t.RowsRead, t.RowsImported, t.RowsSkipped, documentCount, ownerId, disciplineId);
}
