using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Tidps.GetTidpFolder;

// The folder as the database now holds it: owners, the discipline folders under them
// and the files under those, including everything currently Missing. This is the view
// an operator checks a sync against — "is `08. Provisional Sum` really holding six
// files, and is the one I moved last week showing up where I moved it to".
[Permission(Permissions.ReportsView)]
public sealed record GetTidpFolderQuery(Guid ProjectId) : IQuery<TidpFolderDto>;

public sealed record TidpFolderDto(
    Guid ProjectId,
    int OwnerCount,
    int FileCount,
    int MissingCount,
    IReadOnlyList<TidpOwnerDto> Owners);

public sealed record TidpOwnerDto(
    Guid Id,
    string RelativePath,
    string FolderName,
    int? SortOrder,
    string? OwnerName,
    TidpOwnerType OwnerType,
    TidpFolderStatus FolderStatus,
    DateTime? MissingSince,
    IReadOnlyList<TidpDisciplineFolderDto> Disciplines,

    // Files sitting straight under the owner folder, with no discipline folder between
    // — `02.JINGGONG/QF01012-...-STL-...xlsx`. A real shape in the sample, not an edge
    // case, so it gets its own list rather than a null discipline to group under.
    IReadOnlyList<TidpFolderFileDto> Files);

public sealed record TidpDisciplineFolderDto(
    Guid Id,
    string RelativePath,
    string FolderName,
    string DisciplineCode,
    string DisciplineName,
    TidpFolderStatus FolderStatus,
    DateTime? MissingSince,
    IReadOnlyList<TidpFolderFileDto> Files);

public sealed record TidpFolderFileDto(
    Guid Id,
    string RelativePath,
    string FileName,
    string? DisciplineTag,
    string? Sequence,
    string? Zone,
    string? Level,
    long SizeBytes,
    DateTime? LastModifiedUtc,
    DateTime? LastSeenAt,
    string? ContentHash,
    TidpFolderStatus FolderStatus,
    DateTime? MissingSince,
    TidpFileStatus Status,
    string? Error,
    int RowsRead,
    int RowsImported,
    int RowsSkipped,
    int DocumentCount,

    // Kept on the DTO because the tree is assembled from a flat projection: EF can
    // translate one query with the counts in it, not a nested one per folder.
    Guid? OwnerId,
    Guid? FolderDisciplineId);
