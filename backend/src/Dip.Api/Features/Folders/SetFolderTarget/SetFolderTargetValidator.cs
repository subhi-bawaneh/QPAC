using FluentValidation;

namespace Dip.Api.Features.Folders.SetFolderTarget;

public sealed class SetFolderTargetValidator : AbstractValidator<SetFolderTargetCommand>
{
    public SetFolderTargetValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Target).IsInEnum();
    }
}
