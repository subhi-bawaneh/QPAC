using Dip.Domain.Enums;

namespace Dip.Api.Features.Imports;

public sealed record StartImportResult(Guid ImportBatchId, int TotalRows);

public sealed record RunStepResult(
    Guid ImportBatchId,
    int Processed,
    int Total,
    bool Done,
    ImportBatchSummary Batch);

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
    string? Log);
