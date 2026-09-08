using FluentValidation;

namespace Dip.Api.Features.Lists.CreatePicklistItem;

public sealed class CreatePicklistItemValidator : AbstractValidator<CreatePicklistItemCommand>
{
    public CreatePicklistItemValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Field).IsInEnum();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).NotNull().MaximumLength(300);
        RuleFor(x => x.SortOrder).GreaterThan(0).When(x => x.SortOrder is not null);
    }
}
