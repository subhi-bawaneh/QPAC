using FluentValidation;

namespace Dip.Api.Features.Users.AssignRole;

public sealed class AssignRoleValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Roles).NotNull().WithMessage("Provide the exact list of roles to assign (empty list = clear all)");
    }
}
