using FluentValidation;

namespace Dip.Api.Features.Drafts.GetPromoteDiff;

public sealed class GetPromoteDiffValidator : AbstractValidator<GetPromoteDiffQuery>
{
    public GetPromoteDiffValidator()
    {
        RuleFor(x => x.FolderFileId).NotEmpty();
        RuleFor(x => x.MaxRows).InclusiveBetween(1, 2000);
    }
}
