using FluentValidation;

namespace Dip.Api.Features.Folders.TriggerDriveSync;

public sealed class TriggerDriveSyncValidator : AbstractValidator<TriggerDriveSyncCommand>
{
    public TriggerDriveSyncValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
    }
}
