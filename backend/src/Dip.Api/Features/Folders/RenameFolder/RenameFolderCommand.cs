using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.RenameFolder;

[Permission(Permissions.FoldersManage)]
public sealed record RenameFolderCommand(Guid Id, string NewName) : ICommand<Unit>;
