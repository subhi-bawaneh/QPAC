using FluentValidation;

namespace Dip.Api.Features.Lists.ReorderPicklist;

public sealed class ReorderPicklistValidator : AbstractValidator<ReorderPicklistCommand>
{
    public ReorderPicklistValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Field).IsInEnum();
        RuleFor(x => x.Ids).NotEmpty();
        RuleFor(x => x.Ids)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Ids must be unique");
    }
}
