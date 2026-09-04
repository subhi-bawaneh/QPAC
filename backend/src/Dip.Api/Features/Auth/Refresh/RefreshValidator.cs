using FluentValidation;

namespace Dip.Api.Features.Auth.Refresh;

public sealed class RefreshValidator : AbstractValidator<RefreshCommand>
{
    public RefreshValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MinimumLength(20);
    }
}
