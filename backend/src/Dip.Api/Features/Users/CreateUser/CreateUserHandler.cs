using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Dip.Api.Features.Users.CreateUser;

public sealed class CreateUserHandler : ICommandHandler<CreateUserCommand, Guid>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;

    public CreateUserHandler(UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<Guid> Handle(CreateUserCommand command, CancellationToken ct)
    {
        _ = ct;
        var existing = await _users.FindByEmailAsync(command.Email);
        if (existing is not null)
        {
            throw new FluentValidation.ValidationException(
                $"User with email '{command.Email}' already exists");
        }

        foreach (var role in command.Roles)
        {
            if (!await _roles.RoleExistsAsync(role))
            {
                throw new FluentValidation.ValidationException($"Role '{role}' does not exist");
            }
        }

        var user = new ApplicationUser
        {
            UserName = command.Email,
            Email = command.Email,
            EmailConfirmed = true,
            FullName = command.FullName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var create = await _users.CreateAsync(user, command.Password);
        if (!create.Succeeded)
        {
            throw new FluentValidation.ValidationException(
                string.Join("; ", create.Errors.Select(e => e.Description)));
        }

        var addRoles = await _users.AddToRolesAsync(user, command.Roles);
        if (!addRoles.Succeeded)
        {
            throw new ForbiddenException(
                string.Join("; ", addRoles.Errors.Select(e => e.Description)));
        }

        return user.Id;
    }
}
