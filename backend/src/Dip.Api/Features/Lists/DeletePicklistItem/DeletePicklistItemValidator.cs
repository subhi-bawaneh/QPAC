using FluentValidation;

namespace Dip.Api.Features.Lists.DeletePicklistItem;

public sealed class DeletePicklistItemValidator : AbstractValidator<DeletePicklistItemCommand>
{
    public DeletePicklistItemValidator() => RuleFor(x => x.Id).NotEmpty();
}
