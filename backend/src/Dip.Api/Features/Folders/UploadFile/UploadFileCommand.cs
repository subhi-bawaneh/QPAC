using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.UploadFile;

// The bytes travel with the command (not a Stream) because the transaction
// behavior may retry the handler, and because they end up in FileBlob verbatim.
[Permission(Permissions.FoldersManage)]
public sealed record UploadFileCommand(
    Guid FolderId,
    string FileName,
    byte[] Content) : ICommand<UploadResult>;
