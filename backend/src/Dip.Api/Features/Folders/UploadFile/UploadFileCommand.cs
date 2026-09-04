using System.IO;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.UploadFile;

[Permission(Permissions.FoldersManage)]
public sealed record UploadFileCommand(
    Guid FolderId,
    string FileName,
    long SizeBytes,
    Stream Content) : ICommand<Guid>;
