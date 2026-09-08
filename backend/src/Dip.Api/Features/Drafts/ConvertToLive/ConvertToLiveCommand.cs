using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Drafts.ConvertToLive;

// A company moves from working in Drive to working in the system: promote every file
// in the folder subtree, then flip the target (refactor-plan § 3 R7). Repeatable —
// Drive keeps refreshing the drafts, and Convert can be run again.
[Permission(Permissions.FoldersAssignTarget)]
[Permission(Permissions.DraftsPromote)]
public sealed record ConvertToLiveCommand(Guid FolderId) : ICommand<ConvertToLiveResultDto>;

public sealed record ConvertedFileDto(
    Guid FileId,
    string Name,
    int Added,
    int Updated,
    int Deleted,
    int Conflicts);

public sealed record ConvertToLiveResultDto(
    Guid FolderId,
    int FilesConverted,
    int Added,
    int Updated,
    int Deleted,
    int Skipped,
    int Conflicts,
    IReadOnlyList<ConvertedFileDto> PerFile);
