using FluentValidation;

namespace Dip.Api.Features.Drafts.RollbackPromote;

public sealed class RollbackPromoteValidator : AbstractValidator<RollbackPromoteCommand>
{
    public RollbackPromoteValidator()
    {
        RuleFor(x => x.PromoteBatchId).NotEmpty();
    }
}
