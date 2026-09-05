using FluentValidation;

namespace Dip.Api.Features.Drafts.Promote;

public sealed class PromoteValidator : AbstractValidator<PromoteCommand>
{
    public PromoteValidator()
    {
        RuleFor(x => x.FolderFileId).NotEmpty();
    }
}
