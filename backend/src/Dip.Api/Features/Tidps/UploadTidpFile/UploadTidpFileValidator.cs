using FluentValidation;

namespace Dip.Api.Features.Tidps.UploadTidpFile;

public sealed class UploadTidpFileValidator : AbstractValidator<UploadTidpFileCommand>
{
    public UploadTidpFileValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).NotEmpty().WithMessage("The uploaded file is empty");
    }
}
