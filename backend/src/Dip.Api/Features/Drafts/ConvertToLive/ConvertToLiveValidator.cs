using FluentValidation;

namespace Dip.Api.Features.Drafts.ConvertToLive;

public sealed class ConvertToLiveValidator : AbstractValidator<ConvertToLiveCommand>
{
    public ConvertToLiveValidator()
    {
        RuleFor(x => x.FolderId).NotEmpty();
    }
}
