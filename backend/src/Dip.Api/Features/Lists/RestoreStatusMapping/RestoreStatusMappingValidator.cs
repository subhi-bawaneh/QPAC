using FluentValidation;

namespace Dip.Api.Features.Lists.RestoreStatusMapping;

public sealed class RestoreStatusMappingValidator : AbstractValidator<RestoreStatusMappingCommand>
{
    public RestoreStatusMappingValidator() => RuleFor(x => x.Id).NotEmpty();
}
