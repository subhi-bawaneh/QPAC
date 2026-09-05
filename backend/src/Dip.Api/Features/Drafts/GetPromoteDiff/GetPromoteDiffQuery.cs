using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Drafts.GetPromoteDiff;

// Pre-flight for Promote: what would happen, without writing anything.
// Counts are always exact; the row lists are capped at MaxRows per bucket so a
// 16k-row MIDP draft doesn't return a 16k-row payload to a dialog.
[Permission(Permissions.DraftsPromote)]
public sealed record GetPromoteDiffQuery(
    Guid FolderFileId,
    int MaxRows = 200) : IQuery<PromoteDiffDto>;
