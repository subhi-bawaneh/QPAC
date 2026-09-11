using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Tidps.UploadTidpFile;

// Uploading a source file is the super admin's alone (files.manage). The bytes are
// parsed and discarded: nothing is stored on disk or in the database.
[Permission(Permissions.FilesManage)]
public sealed record UploadTidpFileCommand(
    Guid ProjectId,
    string FileName,
    byte[] Content) : ICommand<UploadAccepted>;

// 202: the row exists, the import is queued. The client follows the batch over the hub.
public sealed record UploadAccepted(Guid TidpFileId, Guid BatchId, string FileName);
