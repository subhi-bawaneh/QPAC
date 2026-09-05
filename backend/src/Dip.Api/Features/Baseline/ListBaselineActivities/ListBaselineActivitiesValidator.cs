using FluentValidation;

namespace Dip.Api.Features.Baseline.ListBaselineActivities;

public sealed class ListBaselineActivitiesValidator : AbstractValidator<ListBaselineActivitiesQuery>
{
    public ListBaselineActivitiesValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum().When(x => x.Type is not null);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
        RuleFor(x => x.Search).MaximumLength(200);
    }
}
