using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Projects.ListDisciplines;

// Every discipline the project knows, whether or not a TIDP has been uploaded for it.
// The explorer shows an empty discipline as an empty folder rather than hiding it:
// a discipline with no file is exactly the thing an operator needs to notice.
[Permission(Permissions.ReportsView)]
public sealed record ListDisciplinesQuery(Guid ProjectId) : IQuery<IReadOnlyCollection<DisciplineDto>>;

public sealed record DisciplineDto(Guid Id, string Code, string CorporateName, int FileCount);
