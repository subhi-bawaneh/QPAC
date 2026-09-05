using Dip.Application.Documents;

namespace Dip.Api.Features.Drafts;

// One row as rendered in the promote dialog.
public sealed record PromoteRowDto(
    string DocumentNumber,
    Guid? DraftId,
    Guid? LiveId,
    string? Title,
    IReadOnlyList<DraftFieldChange> Changes,
    string? Reason);

public sealed record PromoteDiffDto(
    Guid FolderFileId,
    Guid? ImportBatchId,
    DateTime? ImportedAt,
    int Added,
    int Modified,
    int Unchanged,
    int Deleted,
    int Conflicts,
    int MaxRows,
    IReadOnlyList<PromoteRowDto> AddedRows,
    IReadOnlyList<PromoteRowDto> ModifiedRows,
    IReadOnlyList<PromoteRowDto> DeletedRows,
    IReadOnlyList<PromoteRowDto> ConflictRows);

public sealed record PromoteResultDto(
    Guid PromoteBatchId,
    Guid FolderFileId,
    int Added,
    int Updated,
    int Deleted,
    int Skipped,
    int Conflicts,
    bool RecalculationRequired);
