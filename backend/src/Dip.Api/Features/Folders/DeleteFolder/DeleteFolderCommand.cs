using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.DeleteFolder;

// Soft-deletes a folder. Refuses while it still holds subfolders or files, so a
// stray click cannot take a synced Drive tree with it — the caller empties it first.
[Permission(Permissions.FoldersManage)]
public sealed record DeleteFolderCommand(Guid Id) : ICommand<Unit>;
