using FluentValidation;

namespace Dip.Api.Features.Lists.CreateStatusMapping;

public sealed class CreateStatusMappingValidator : AbstractValidator<CreateStatusMappingCommand>
{
    public CreateStatusMappingValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.AconexStatus).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum();
    }
}
