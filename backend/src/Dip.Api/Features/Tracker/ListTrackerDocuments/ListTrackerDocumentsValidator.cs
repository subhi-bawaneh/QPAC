using FluentValidation;

namespace Dip.Api.Features.Tracker.ListTrackerDocuments;

public sealed class ListTrackerDocumentsValidator : AbstractValidator<ListTrackerDocumentsQuery>
{
    public ListTrackerDocumentsValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.Discipline).MaximumLength(100);
    }
}
