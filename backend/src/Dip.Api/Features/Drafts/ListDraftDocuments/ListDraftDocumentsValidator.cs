using FluentValidation;

namespace Dip.Api.Features.Drafts.ListDraftDocuments;

public sealed class ListDraftDocumentsValidator : AbstractValidator<ListDraftDocumentsQuery>
{
    public ListDraftDocumentsValidator()
    {
        RuleFor(x => x.FolderFileId).NotEmpty();
        RuleFor(x => x.State).IsInEnum().When(x => x.State is not null);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
        RuleFor(x => x.Search).MaximumLength(200);
    }
}
