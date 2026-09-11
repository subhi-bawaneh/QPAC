using FluentValidation;

namespace Dip.Api.Features.Tidps.ReplaceTidpFile;

public sealed class ReplaceTidpFileValidator : AbstractValidator<ReplaceTidpFileCommand>
{
    public ReplaceTidpFileValidator()
    {
        RuleFor(x => x.TidpFileId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).NotEmpty().WithMessage("The uploaded file is empty");
    }
}
