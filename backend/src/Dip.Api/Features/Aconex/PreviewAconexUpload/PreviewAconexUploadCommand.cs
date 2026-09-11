using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Aconex.PreviewAconexUpload;

// Parses the uploaded files and counts what is new, writing nothing. The client keeps
// the files and re-sends them to confirm; the server re-parses rather than holding a
// copy, because holding one would mean storing bytes the system does not store.
[Permission(Permissions.FilesManage)]
public sealed record PreviewAconexUploadCommand(
    Guid ProjectId,
    IReadOnlyList<UploadedFile> Files) : ICommand<AconexPreview>;

public sealed record UploadedFile(string FileName, byte[] Content);
