using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.CreateFolder;

[Permission(Permissions.FoldersManage)]
public sealed record CreateFolderCommand(Guid ProjectId, Guid? ParentId, string Name) : ICommand<Guid>;
