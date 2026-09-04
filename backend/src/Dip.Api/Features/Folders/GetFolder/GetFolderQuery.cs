using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.GetFolder;

[Permission(Permissions.ReportsView)]
public sealed record GetFolderQuery(Guid Id) : IQuery<FolderDetail>;
