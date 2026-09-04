using System.Security.Claims;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Identity;
using Dip.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Users.SetDisciplines;

public sealed class SetDisciplinesHandler : ICommandHandler<SetDisciplinesCommand, Unit>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly DipDbContext _db;

    public SetDisciplinesHandler(UserManager<ApplicationUser> users, DipDbContext db)
    {
        _users = users;
        _db = db;
    }

    public async Task<Unit> Handle(SetDisciplinesCommand command, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(command.UserId.ToString())
            ?? throw new KeyNotFoundException($"User {command.UserId} not found");

        // Validate that all discipline codes are known for this project's active discipline set.
        var knownCodes = await _db.Disciplines
            .Select(d => d.Code)
            .ToListAsync(ct);
        var known = knownCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknown = command.DisciplineCodes
            .Where(code => !known.Contains(code))
            .ToArray();
        if (unknown.Length > 0)
        {
            throw new FluentValidation.ValidationException(
                $"Unknown discipline code(s): {string.Join(", ", unknown)}");
        }

        var currentClaims = await _users.GetClaimsAsync(user);
        var currentDisciplines = currentClaims
            .Where(c => c.Type == "discipline")
            .ToList();

        // Remove stale, add missing (rather than clear-then-add so audit is cleaner).
        var expected = new HashSet<string>(command.DisciplineCodes, StringComparer.OrdinalIgnoreCase);
        foreach (var stale in currentDisciplines.Where(c => !expected.Contains(c.Value)))
        {
            await _users.RemoveClaimAsync(user, stale);
        }

        var existing = currentDisciplines.Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var missing in command.DisciplineCodes.Where(code => !existing.Contains(code)))
        {
            await _users.AddClaimAsync(user, new Claim("discipline", missing));
        }

        return Unit.Value;
    }
}
