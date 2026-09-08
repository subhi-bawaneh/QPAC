using FluentValidation;

namespace Dip.Api.Features.Lists.GetStatusMappings;

public sealed class GetStatusMappingsValidator : AbstractValidator<GetStatusMappingsQuery>
{
    public GetStatusMappingsValidator() => RuleFor(x => x.ProjectId).NotEmpty();
}
