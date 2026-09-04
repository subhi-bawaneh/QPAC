using FluentValidation;

namespace Dip.Api.Features.Folders.RenameFolder;

public sealed class RenameFolderValidator : AbstractValidator<RenameFolderCommand>
{
    public RenameFolderValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NewName)
            .NotEmpty()
            .MaximumLength(300)
            .Must(n => !n.Contains('/', StringComparison.Ordinal)).WithMessage("Folder name cannot contain '/'");
    }
}
