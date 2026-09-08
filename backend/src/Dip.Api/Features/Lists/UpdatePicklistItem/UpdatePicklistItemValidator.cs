using FluentValidation;

namespace Dip.Api.Features.Lists.UpdatePicklistItem;

public sealed class UpdatePicklistItemValidator : AbstractValidator<UpdatePicklistItemCommand>
{
    public UpdatePicklistItemValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).NotNull().MaximumLength(300);
        RuleFor(x => x.SortOrder).GreaterThan(0);
    }
}
