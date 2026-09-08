using FluentValidation;

namespace Dip.Api.Features.Lists.GetPicklists;

public sealed class GetPicklistsValidator : AbstractValidator<GetPicklistsQuery>
{
    public GetPicklistsValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
    }
}
