using FluentValidation;

namespace Dip.Api.Features.Recalculation.RunRecalculationStep;

public sealed class RunRecalculationStepValidator : AbstractValidator<RunRecalculationStepCommand>
{
    public RunRecalculationStepValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).InclusiveBetween(1, RecalculationService.MaxChunkSize);
    }
}
