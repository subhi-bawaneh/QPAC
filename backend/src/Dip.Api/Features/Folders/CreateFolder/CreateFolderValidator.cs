using FluentValidation;

namespace Dip.Api.Features.Folders.CreateFolder;

public sealed class CreateFolderValidator : AbstractValidator<CreateFolderCommand>
{
    public CreateFolderValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(300)
            .Must(n => !n.Contains('/', StringComparison.Ordinal)).WithMessage("Folder name cannot contain '/'");
    }
}
