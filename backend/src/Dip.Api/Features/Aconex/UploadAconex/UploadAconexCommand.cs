using Dip.Api.Features.Aconex.PreviewAconexUpload;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Aconex.UploadAconex;

// Several exports at once, one batch each. Every one appends: nothing is ever deleted,
// and a line the project already holds is dropped and counted.
[Permission(Permissions.FilesManage)]
public sealed record UploadAconexCommand(
    Guid ProjectId,
    IReadOnlyList<UploadedFile> Files) : ICommand<AconexUploadAccepted>;
