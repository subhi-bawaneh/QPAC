using FluentValidation;

namespace Dip.Api.Features.Tidps.SyncTidpFolder;

public sealed class SyncTidpFolderValidator : AbstractValidator<SyncTidpFolderCommand>
{
    public SyncTidpFolderValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.RootName).MaximumLength(400);

        // An empty upload is refused rather than processed: it is indistinguishable
        // from an upload of a folder that has been emptied, and processing it would
        // mark every file in the project Missing.
        RuleFor(x => x.Files)
            .NotEmpty().WithMessage("The upload carries no files");

        RuleForEach(x => x.Files).ChildRules(file =>
        {
            file.RuleFor(f => f.RelativePath)
                .NotEmpty().WithMessage("A file has no relative path")
                .MaximumLength(1000);
            file.RuleFor(f => f.Content)
                .NotEmpty().WithMessage(f => $"'{f.RelativePath}' is empty");
        });
    }
}
