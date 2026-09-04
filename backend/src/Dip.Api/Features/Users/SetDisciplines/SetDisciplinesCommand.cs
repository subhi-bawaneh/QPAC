using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Users.SetDisciplines;

[Permission(Permissions.UsersManage)]
public sealed record SetDisciplinesCommand(Guid UserId, IReadOnlyCollection<string> DisciplineCodes) : ICommand<Unit>;
