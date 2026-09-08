using FluentValidation;

namespace Dip.Api.Features.Folders.SetFolderCompany;

public sealed class SetFolderCompanyValidator : AbstractValidator<SetFolderCompanyCommand>
{
    public SetFolderCompanyValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AuthorId)
            .Null()
            .When(x => !x.IsCompany)
            .WithMessage("Only a company folder can carry an author");
    }
}
