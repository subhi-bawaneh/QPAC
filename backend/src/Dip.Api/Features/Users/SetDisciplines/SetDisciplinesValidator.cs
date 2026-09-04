using FluentValidation;

namespace Dip.Api.Features.Users.SetDisciplines;

public sealed class SetDisciplinesValidator : AbstractValidator<SetDisciplinesCommand>
{
    public SetDisciplinesValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DisciplineCodes).NotNull();
    }
}
