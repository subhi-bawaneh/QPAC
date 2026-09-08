using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Imports;

public sealed record ImportBatchSummary(
    Guid Id,
    Guid ProjectId,
    ImportKind Kind,
    DataTarget Target,
    Guid? FolderFileId,
    string FileName,
    DateTime ImportedAt,
    string ImportedBy,
    int RowsRead,
    int RowsInserted,
    int RowsUpdated,
    int RowsSkipped,
    bool Completed,
    string? Log)
{
    public static ImportBatchSummary From(ImportBatch b) => new(
        b.Id, b.ProjectId, b.Kind, b.Target, b.FolderFileId, b.FileName,
        b.ImportedAt, b.ImportedBy, b.RowsRead, b.RowsInserted, b.RowsUpdated,
        b.RowsSkipped, b.Completed, b.Log);
}
