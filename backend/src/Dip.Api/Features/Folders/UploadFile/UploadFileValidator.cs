using Dip.Application.Files;
using Dip.Domain.Enums;
using FluentValidation;

namespace Dip.Api.Features.Folders.UploadFile;

public sealed class UploadFileValidator : AbstractValidator<UploadFileCommand>
{
    public const long MaxBytes = 64L * 1024 * 1024;

    private static readonly string[] AllowedExtensions = [".xlsx", ".xlsm", ".xls"];

    public UploadFileValidator()
    {
        RuleFor(x => x.FolderId).NotEmpty();
        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(500)
            .Must(name => AllowedExtensions.Contains(
                Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Only .xlsx, .xlsm and .xls workbooks can be uploaded")
            .Must(name => FileKindDetector.Detect(name) != FileKind.Unknown)
            .WithMessage("Unrecognised workbook name — it must identify a TIDP, MIDP, Tracker, "
                + "Baseline, Picklists or Lists workbook");
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("No file supplied")
            .Must(c => c.LongLength <= MaxBytes).WithMessage("The workbook exceeds the 64 MB limit");
    }
}
