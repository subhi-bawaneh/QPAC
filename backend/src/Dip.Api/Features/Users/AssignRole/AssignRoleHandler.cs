using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Dip.Api.Features.Users.AssignRole;

public sealed class AssignRoleHandler : ICommandHandler<AssignRoleCommand, Unit>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;

    public AssignRoleHandler(UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<Unit> Handle(AssignRoleCommand command, CancellationToken ct)
    {
        _ = ct;
        var user = await _users.FindByIdAsync(command.UserId.ToString())
            ?? throw new KeyNotFoundException($"User {command.UserId} not found");

        foreach (var role in command.Roles)
        {
            if (!await _roles.RoleExistsAsync(role))
            {
                throw new FluentValidation.ValidationException($"Role '{role}' does not exist");
            }
        }

        var current = await _users.GetRolesAsync(user);
        var toRemove = current.Except(command.Roles, StringComparer.Ordinal).ToArray();
        var toAdd = command.Roles.Except(current, StringComparer.Ordinal).ToArray();

        if (toRemove.Length > 0)
        {
            var removeResult = await _users.RemoveFromRolesAsync(user, toRemove);
            if (!removeResult.Succeeded)
            {
                throw new ForbiddenException(string.Join("; ", removeResult.Errors.Select(e => e.Description)));
            }
        }

        if (toAdd.Length > 0)
        {
            var addResult = await _users.AddToRolesAsync(user, toAdd);
            if (!addResult.Succeeded)
            {
                throw new ForbiddenException(string.Join("; ", addResult.Errors.Select(e => e.Description)));
            }
        }

        return Unit.Value;
    }
}
