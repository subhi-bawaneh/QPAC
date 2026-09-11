using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Tidps.DeleteTidpFile;

// Removes a file's rows from the register. Same two protections as a replace: the
// dialog counts the edited rows, and every row is dumped to AuditLogs first.
[Permission(Permissions.FilesManage)]
public sealed record DeleteTidpFileCommand(Guid TidpFileId) : ICommand<DeleteResult>;

public sealed record DeleteResult(Guid TidpFileId, int DocumentsRemoved, int AuditRowsWritten);
