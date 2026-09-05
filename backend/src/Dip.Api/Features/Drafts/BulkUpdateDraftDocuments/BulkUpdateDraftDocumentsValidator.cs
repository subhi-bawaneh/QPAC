using FluentValidation;

namespace Dip.Api.Features.Drafts.BulkUpdateDraftDocuments;

public sealed class BulkUpdateDraftDocumentsValidator : AbstractValidator<BulkUpdateDraftDocumentsCommand>
{
    // Chunked-by-design (hard rule 9): a single call stays well inside one request.
    public const int MaxIds = 2000;

    public BulkUpdateDraftDocumentsValidator()
    {
        RuleFor(x => x.Ids).NotEmpty();
        RuleFor(x => x.Ids.Count).LessThanOrEqualTo(MaxIds)
            .WithMessage($"At most {MaxIds} rows per bulk update");
        RuleFor(x => x.Ids).Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("Ids must not contain empty GUIDs");

        RuleFor(x => x.Fields).NotNull();
        RuleFor(x => x.Fields).Must(f => f.HasAny)
            .WithMessage("At least one field must be set")
            .When(x => x.Fields is not null);

        When(x => x.Fields is not null, () =>
        {
            RuleFor(x => x.Fields.ExtractedFromModel).MaximumLength(100);
            RuleFor(x => x.Fields.ScopeArea).MaximumLength(200);
            RuleFor(x => x.Fields.AuthoringSoftware).MaximumLength(100);
            RuleFor(x => x.Fields.ExchangeFormat).MaximumLength(100);
            RuleFor(x => x.Fields.Scale).MaximumLength(50);
            RuleFor(x => x.Fields.PackageName).MaximumLength(200);
            RuleFor(x => x.Fields.ActivityId).MaximumLength(50);
            RuleFor(x => x.Fields.ClassificationCode).MaximumLength(50);
            RuleFor(x => x.Fields.CorporateDiscipline).NotEmpty().MaximumLength(100)
                .When(f => f.Fields.CorporateDiscipline is not null);
            RuleFor(x => x.Fields.F02Originator).MaximumLength(20);
            RuleFor(x => x.Fields.F03Contract).MaximumLength(20);
            RuleFor(x => x.Fields.F04DocType).NotEmpty().MaximumLength(20)
                .When(f => f.Fields.F04DocType is not null);
            RuleFor(x => x.Fields.F05Discipline).NotEmpty().MaximumLength(20)
                .When(f => f.Fields.F05Discipline is not null);
            RuleFor(x => x.Fields.F06Zone).MaximumLength(20);
            RuleFor(x => x.Fields.F07Building).MaximumLength(50);
            RuleFor(x => x.Fields.F08ADrawingType).MaximumLength(5);
            RuleFor(x => x.Fields.F08BLevel).MaximumLength(10);
        });
    }
}
