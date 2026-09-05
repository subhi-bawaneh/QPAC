using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

public class DraftDiffTests
{
    private static Document LiveDocument() => new()
    {
        Id = Guid.NewGuid(),
        DocumentNumber = "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004",
        Title = "GENERAL NOTES",
        PackageName = "PKG-01",
        ActivityId = "QP.M.GN.GEN.GEN.1400",
        CorporateDiscipline = "Structural",
        DeliveryMilestone = new DateTime(2025, 10, 30, 0, 0, 0, DateTimeKind.Unspecified),
        Scale = "1:100",
        AuthoringSoftware = "Revit",
        ExchangeFormat = "PDF",
    };

    private static DocumentDraft DraftMatching(Document live) => new()
    {
        DocumentNumber = live.DocumentNumber,
        Title = live.Title,
        PackageName = live.PackageName,
        ActivityId = live.ActivityId,
        CorporateDiscipline = live.CorporateDiscipline,
        DeliveryMilestone = live.DeliveryMilestone,
        Scale = live.Scale,
        AuthoringSoftware = live.AuthoringSoftware,
        ExchangeFormat = live.ExchangeFormat,
    };

    [Fact]
    public void StateFor_NoLiveCounterpart_IsNew()
    {
        var draft = DraftMatching(LiveDocument());

        DraftDiff.StateFor(null, DraftDiff.From(draft)).Should().Be(DraftRowState.New);
    }

    [Fact]
    public void StateFor_IdenticalFields_IsUnchanged()
    {
        var live = LiveDocument();
        var draft = DraftMatching(live);

        DraftDiff.StateFor(DraftDiff.From(live), DraftDiff.From(draft))
            .Should().Be(DraftRowState.Unchanged);
    }

    [Fact]
    public void StateFor_ChangedTitle_IsModified()
    {
        var live = LiveDocument();
        var draft = DraftMatching(live);
        draft.Title = "GENERAL NOTES - REV B";

        DraftDiff.StateFor(DraftDiff.From(live), DraftDiff.From(draft))
            .Should().Be(DraftRowState.Modified);
    }

    [Fact]
    public void StateFor_CaseOnlyDifference_IsModified()
    {
        var live = LiveDocument();
        var draft = DraftMatching(live);
        draft.CorporateDiscipline = "structural";

        DraftDiff.StateFor(DraftDiff.From(live), DraftDiff.From(draft))
            .Should().Be(DraftRowState.Modified);
    }

    [Fact]
    public void Changes_ListsOnlyTheFieldsThatDiffer()
    {
        var live = LiveDocument();
        var draft = DraftMatching(live);
        draft.PackageName = "PKG-02";
        draft.DeliveryMilestone = new DateTime(2025, 11, 30, 0, 0, 0, DateTimeKind.Unspecified);

        var changes = DraftDiff.Changes(DraftDiff.From(live), DraftDiff.From(draft));

        changes.Select(c => c.Field).Should().BeEquivalentTo(
            new[] { nameof(Document.PackageName), nameof(Document.DeliveryMilestone) });
        changes.Single(c => c.Field == nameof(Document.PackageName))
            .Should().BeEquivalentTo(new DraftFieldChange(nameof(Document.PackageName), "PKG-01", "PKG-02"));
    }

    [Fact]
    public void Changes_NullVsEmptyString_CountsAsAChange()
    {
        var live = LiveDocument();
        live.Scale = null;
        var draft = DraftMatching(live);
        draft.Scale = string.Empty;

        DraftDiff.Changes(DraftDiff.From(live), DraftDiff.From(draft))
            .Should().ContainSingle(c => c.Field == nameof(Document.Scale));
    }

    // Sub-second precision must survive into the diff payload (hard rule 6).
    [Fact]
    public void Changes_DateFormatting_KeepsSubSecondPrecision()
    {
        var live = LiveDocument();
        var draft = DraftMatching(live);
        draft.DeliveryMilestone = live.DeliveryMilestone!.Value.AddTicks(1234567);

        var change = DraftDiff.Changes(DraftDiff.From(live), DraftDiff.From(draft))
            .Single(c => c.Field == nameof(Document.DeliveryMilestone));

        change.NewValue.Should().Be("2025-10-30T00:00:00.1234567");
    }
}
