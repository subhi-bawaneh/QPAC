using FluentValidation;

namespace Dip.Api.Features.Folders.GetDriveStatus;

public sealed class GetDriveStatusValidator : AbstractValidator<GetDriveStatusQuery>
{
    public GetDriveStatusValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
    }
}
