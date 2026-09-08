using FluentValidation;

namespace Dip.Api.Features.Lists.DeleteStatusMapping;

public sealed class DeleteStatusMappingValidator : AbstractValidator<DeleteStatusMappingCommand>
{
    public DeleteStatusMappingValidator() => RuleFor(x => x.Id).NotEmpty();
}
