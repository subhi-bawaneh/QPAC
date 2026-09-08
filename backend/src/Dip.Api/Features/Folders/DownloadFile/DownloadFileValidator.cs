using FluentValidation;

namespace Dip.Api.Features.Folders.DownloadFile;

public sealed class DownloadFileValidator : AbstractValidator<DownloadFileQuery>
{
    public DownloadFileValidator()
    {
        RuleFor(x => x.FileId).NotEmpty();
    }
}
