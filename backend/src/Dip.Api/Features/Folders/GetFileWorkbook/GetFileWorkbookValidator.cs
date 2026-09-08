using FluentValidation;

namespace Dip.Api.Features.Folders.GetFileWorkbook;

public sealed class GetFileWorkbookValidator : AbstractValidator<GetFileWorkbookQuery>
{
    public GetFileWorkbookValidator()
    {
        RuleFor(x => x.FileId).NotEmpty();
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 2000);
    }
}
