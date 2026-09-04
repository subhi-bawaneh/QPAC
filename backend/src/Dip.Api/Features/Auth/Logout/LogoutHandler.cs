using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Auth.Logout;

public sealed class LogoutHandler : ICommandHandler<LogoutCommand, Unit>
{
    private readonly DipDbContext _db;

    public LogoutHandler(DipDbContext db) => _db = db;

    public async Task<Unit> Handle(LogoutCommand command, CancellationToken ct)
    {
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == command.RefreshToken, ct);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = command.RequestIp;
        }

        return Unit.Value;
    }
}
