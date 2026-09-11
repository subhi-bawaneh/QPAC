using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Api.Features.Tidps.UploadTidpFile;

namespace Dip.Api.Features.Tidps.ReplaceTidpFile;

// Replacement is total and does not spare hand edits — the owner's decision. Two things
// soften it: GetReplacePreview counts the edited rows the dialog shows, and every
// outgoing row is written to AuditLogs before the delete, so recovery is a query.
[Permission(Permissions.FilesManage)]
public sealed record ReplaceTidpFileCommand(
    Guid TidpFileId,
    string FileName,
    byte[] Content) : ICommand<UploadAccepted>;
