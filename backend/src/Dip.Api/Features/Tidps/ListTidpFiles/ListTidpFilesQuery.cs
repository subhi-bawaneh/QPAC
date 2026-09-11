using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Tidps.ListTidpFiles;

[Permission(Permissions.ReportsView)]
public sealed record ListTidpFilesQuery(Guid ProjectId) : IQuery<IReadOnlyCollection<TidpFileDto>>;
