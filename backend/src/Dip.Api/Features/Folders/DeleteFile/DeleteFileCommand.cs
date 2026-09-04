using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.DeleteFile;

[Permission(Permissions.FoldersManage)]
public sealed record DeleteFileCommand(Guid Id) : ICommand<Unit>;
