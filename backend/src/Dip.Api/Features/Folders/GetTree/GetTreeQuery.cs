using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.GetTree;

[Permission(Permissions.ReportsView)]
public sealed record GetTreeQuery(Guid ProjectId) : IQuery<IReadOnlyList<FolderTreeNode>>;
