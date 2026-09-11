using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Imports;

// One upload as the dashboard and the Aconex page render it. RowsDuplicate is the
// number an Aconex append dropped because it already held the line, which is the
// figure that tells an operator a re-upload of an overlapping export did nothing.
public sealed record ImportBatchSummary(
    Guid Id,
    Guid ProjectId,
    ImportKind Kind,
    Guid? TidpFileId,
    string FileName,
    DateTime ImportedAt,
    string UploadedBy,
    int RowsRead,
    int RowsInserted,
    int RowsUpdated,
    int RowsSkipped,
    int RowsDuplicate,
    ImportBatchStatus Status,
    string? Log)
{
    public static ImportBatchSummary From(ImportBatch b) => new(
        b.Id, b.ProjectId, b.Kind, b.TidpFileId, b.FileName,
        b.ImportedAt, b.UploadedBy, b.RowsRead, b.RowsInserted, b.RowsUpdated,
        b.RowsSkipped, b.RowsDuplicate, b.Status, b.Log);
}
