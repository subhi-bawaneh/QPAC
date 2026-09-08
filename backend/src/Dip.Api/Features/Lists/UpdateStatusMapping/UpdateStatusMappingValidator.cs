using FluentValidation;

namespace Dip.Api.Features.Lists.UpdateStatusMapping;

public sealed class UpdateStatusMappingValidator : AbstractValidator<UpdateStatusMappingCommand>
{
    public UpdateStatusMappingValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AconexStatus).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum();
    }
}
