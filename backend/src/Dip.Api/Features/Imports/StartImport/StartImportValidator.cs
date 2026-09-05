using FluentValidation;

namespace Dip.Api.Features.Imports.StartImport;

public sealed class StartImportValidator : AbstractValidator<StartImportCommand>
{
    public StartImportValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.FolderFileId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Target).IsInEnum();
    }
}
