using FluentValidation;

namespace Dip.Api.Features.Projects.UpdateProjectSettings;

public sealed class UpdateProjectSettingsValidator : AbstractValidator<UpdateProjectSettingsCommand>
{
    public UpdateProjectSettingsValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.ScheduleMode).IsInEnum();

        // Working Plan adds this many days to a delivery milestone for the planned
        // finish; a year is far beyond anything sensible.
        RuleFor(x => x.WorkingPlanApprovalDays).InclusiveBetween(1, 365);

        // The progress weights are fractions of a document's budget, and they only
        // make sense in ascending order.
        RuleFor(x => x.WeightPending).InclusiveBetween(0m, 1m);
        RuleFor(x => x.WeightSub1).InclusiveBetween(0m, 1m);
        RuleFor(x => x.WeightSub2).InclusiveBetween(0m, 1m);
        RuleFor(x => x.WeightApproved).InclusiveBetween(0m, 1m);

        RuleFor(x => x)
            .Must(x => x.WeightPending <= x.WeightSub1
                && x.WeightSub1 <= x.WeightSub2
                && x.WeightSub2 <= x.WeightApproved)
            .WithMessage("Weights must not decrease: pending <= sub 1 <= sub 2 <= approved");
    }
}
