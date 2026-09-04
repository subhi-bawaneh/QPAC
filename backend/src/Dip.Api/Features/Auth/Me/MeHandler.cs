using Dip.Application.Abstractions;
using Dip.Application.Behaviors;

namespace Dip.Api.Features.Auth.Me;

public sealed class MeHandler : IQueryHandler<MeQuery, UserSummary>
{
    private readonly ICurrentUser _currentUser;

    public MeHandler(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<UserSummary> Handle(MeQuery query, CancellationToken ct)
    {
        _ = query;
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new UnauthorizedException("Not authenticated");
        }

        var summary = new UserSummary(
            _currentUser.UserId.Value,
            _currentUser.UserName ?? string.Empty,
            _currentUser.FullName ?? _currentUser.UserName ?? string.Empty,
            _currentUser.Roles,
            _currentUser.Permissions,
            _currentUser.DisciplineCodes);
        return Task.FromResult(summary);
    }
}
