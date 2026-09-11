using Dip.Domain.Enums;

namespace Dip.Api.Features.Tidps;

// One uploaded TIDP workbook as the explorer renders it. DisciplineName is what the
// page groups by, so it is projected here rather than left to a second request.
public sealed record TidpFileDto(
    Guid Id,
    Guid ProjectId,
    Guid DisciplineId,
    string DisciplineCode,
    string DisciplineName,
    string FileName,
    string DocumentReference,
    string RevisionNumber,
    int RowsRead,
    int RowsImported,
    int RowsSkipped,
    int DocumentCount,
    int EditedCount,
    string UploadedBy,
    DateTime UploadedAt,
    TidpFileStatus Status,
    string? Error);
