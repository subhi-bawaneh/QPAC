using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Projects.GetProjectSettings;

[Permission(Permissions.ReportsView)]
public sealed record GetProjectSettingsQuery(Guid ProjectId) : IQuery<ProjectSettingsDto>;
