using FluentValidation;

namespace Dip.Api.Features.Lists.RestorePicklistItem;

public sealed class RestorePicklistItemValidator : AbstractValidator<RestorePicklistItemCommand>
{
    public RestorePicklistItemValidator() => RuleFor(x => x.Id).NotEmpty();
}
