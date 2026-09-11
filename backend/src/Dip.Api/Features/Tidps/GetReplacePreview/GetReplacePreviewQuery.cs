using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Tidps.GetReplacePreview;

// What a replace or a delete will destroy. EditedRows is a real count, not a warning
// string: the operator is told how much hand-entered work is about to go, because the
// replace itself does not spare it.
[Permission(Permissions.FilesManage)]
public sealed record GetReplacePreviewQuery(Guid TidpFileId) : IQuery<ReplacePreview>;

public sealed record ReplacePreview(
    Guid TidpFileId,
    string FileName,
    string DisciplineName,
    int Rows,
    int EditedRows,
    DateTime UploadedAt,
    string UploadedBy);
