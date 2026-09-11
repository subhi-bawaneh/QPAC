using FluentValidation;


namespace Dip.Api.Features.Documents.UpdateDocument;

public sealed class UpdateDocumentValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);

        // Same three fields the importer treats as mandatory for a numberable row.
        RuleFor(x => x.F01Project).NotEmpty().MaximumLength(20);
        RuleFor(x => x.F04DocType).NotEmpty().MaximumLength(20);
        RuleFor(x => x.F05Discipline).NotEmpty().MaximumLength(20);

        RuleFor(x => x.F02Originator).MaximumLength(20);
        RuleFor(x => x.F03Contract).MaximumLength(20);
        RuleFor(x => x.F06Zone).MaximumLength(20);
        RuleFor(x => x.F07Building).MaximumLength(50);
        RuleFor(x => x.F08ADrawingType).MaximumLength(5);
        RuleFor(x => x.F08BLevel).MaximumLength(10);
        RuleFor(x => x.F08CSequence).MaximumLength(10);

        RuleFor(x => x.CorporateDiscipline).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExtractedFromModel).MaximumLength(100);
        RuleFor(x => x.ScopeArea).MaximumLength(200);
        RuleFor(x => x.AuthoringSoftware).MaximumLength(100);
        RuleFor(x => x.ExchangeFormat).MaximumLength(100);
        RuleFor(x => x.Scale).MaximumLength(50);
        RuleFor(x => x.PackageName).MaximumLength(200);
        RuleFor(x => x.ActivityId).MaximumLength(50);
        RuleFor(x => x.ClassificationCode).MaximumLength(50);

        RuleForEach(x => x.Exchanges).ChildRules(e =>
        {
            e.RuleFor(x => x.Number).InclusiveBetween(1, 2);
            e.RuleFor(x => x.DurationDays).GreaterThanOrEqualTo(0).When(x => x.DurationDays is not null);
            e.RuleFor(x => x.Author).MaximumLength(200);
            e.RuleFor(x => x.Stage).MaximumLength(50);
            e.RuleFor(x => x.ProgrammeRef).MaximumLength(50);
            e.RuleFor(x => x.Geometrical).MaximumLength(50);
            e.RuleFor(x => x.NonGeometrical).MaximumLength(200);
            e.RuleFor(x => x.Predecessor).MaximumLength(200);
        }).When(x => x.Exchanges is not null);

        RuleFor(x => x.Exchanges!)
            .Must(list => list.Select(e => e.Number).Distinct().Count() == list.Count)
            .WithMessage("Exchange numbers must be unique")
            .When(x => x.Exchanges is not null);
    }
}
