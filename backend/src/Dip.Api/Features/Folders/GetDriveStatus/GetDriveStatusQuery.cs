using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.GetDriveStatus;

[Permission(Permissions.ReportsView)]
public sealed record GetDriveStatusQuery(Guid ProjectId) : IQuery<DriveStatusDto>;
