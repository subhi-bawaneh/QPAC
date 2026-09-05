using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Drafts.ListDraftDocuments;

// Paged listing of one file's draft rows. FolderFileId is required — a draft is
// always reviewed per uploaded file, and a MIDP draft alone holds ~16k rows.
[Permission(Permissions.ReportsView)]
public sealed record ListDraftDocumentsQuery(
    Guid FolderFileId,
    DraftRowState? State = null,
    Guid? DisciplineId = null,
    bool? IsDuplicate = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<DraftDocumentDto>>;
