using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Drafts.Promote;

// Writes one Draft file into the Live layer. Conflicting rows (and duplicates
// within the file) are never written — they are reported back and left in the
// draft marked Conflict, so the rest of the file still promotes.
//
// deleteMissing removes Live rows that came from THIS file and are no longer in
// the draft (PLAN.md § 3.4); without it they are counted and left alone.
[Permission(Permissions.DraftsPromote)]
public sealed record PromoteCommand(
    Guid FolderFileId,
    bool DeleteMissing = false) : ICommand<PromoteResultDto>;
