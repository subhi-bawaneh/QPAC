using Dip.Application.Documents;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

public class PromoteClassifierTests
{
    private static readonly Guid ThisFile = Guid.NewGuid();
    private static readonly Guid OtherFile = Guid.NewGuid();
    private static readonly DateTime ImportedAt = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private const string Number = "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004";

    private static DraftComparableFields Fields(string title = "GENERAL NOTES") => new(
        Title: title,
        ExtractedFromModel: "Yes",
        ScopeArea: "ZONE-A",
        PackageName: "PKG-01",
        ActivityId: "QP.M.GN.GEN.GEN.1400",
        ClassificationCode: "FI_60_25",
        CorporateDiscipline: "Structural",
        DeliveryMilestone: new DateTime(2025, 10, 30, 0, 0, 0, DateTimeKind.Unspecified),
        Scale: "1:100",
        AuthoringSoftware: "Revit",
        ExchangeFormat: "PDF",
        BudgetWeight: 1m);

    private static PromoteDraftRow Draft(
        string number = Number, bool isDuplicate = false,
        string title = "GENERAL NOTES", string signature = "1|;") =>
        new(Guid.NewGuid(), number, isDuplicate, Fields(title), signature);

    private static PromoteLiveRow Live(
        string number = Number, Guid? folderFileId = null, DateTime? updatedAt = null,
        string title = "GENERAL NOTES", string signature = "1|;") =>
        new(Guid.NewGuid(), number, folderFileId ?? ThisFile,
            updatedAt ?? ImportedAt.AddMinutes(-5), Fields(title), signature);

    private static IReadOnlyList<PromoteDecision> Classify(
        IEnumerable<PromoteDraftRow> drafts, IEnumerable<PromoteLiveRow> live) =>
        PromoteClassifier.Classify(drafts, live, ThisFile, ImportedAt);

    [Fact]
    public void NoLiveRow_IsAdded()
    {
        var decisions = Classify(new[] { Draft() }, Array.Empty<PromoteLiveRow>());

        decisions.Should().ContainSingle()
            .Which.Action.Should().Be(PromoteAction.Add);
    }

    [Fact]
    public void IdenticalLiveRow_IsUnchanged()
    {
        var decisions = Classify(new[] { Draft() }, new[] { Live() });

        decisions.Should().ContainSingle().Which.Action.Should().Be(PromoteAction.Unchanged);
    }

    [Fact]
    public void ChangedField_IsUpdateAndCarriesTheChange()
    {
        var decisions = Classify(new[] { Draft(title: "NEW TITLE") }, new[] { Live(title: "OLD TITLE") });

        var decision = decisions.Should().ContainSingle().Subject;
        decision.Action.Should().Be(PromoteAction.Update);
        decision.Changes.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new DraftFieldChange("Title", "OLD TITLE", "NEW TITLE"));
    }

    // A moved exchange date is a real planning change even though every scalar matches.
    [Fact]
    public void ChangedExchangeSignatureOnly_IsUpdate()
    {
        var decisions = Classify(
            new[] { Draft(signature: "1|||JINGGONG|||5||2026-01-15;") },
            new[] { Live(signature: "1|||JINGGONG|||5||2025-12-15;") });

        var decision = decisions.Should().ContainSingle().Subject;
        decision.Action.Should().Be(PromoteAction.Update);
        decision.Changes.Should().ContainSingle().Which.Field.Should().Be("Exchanges");
    }

    [Fact]
    public void LiveRowFromAnotherFile_IsConflict()
    {
        var decisions = Classify(new[] { Draft() }, new[] { Live(folderFileId: OtherFile) });

        var decision = decisions.Should().ContainSingle().Subject;
        decision.Action.Should().Be(PromoteAction.Conflict);
        decision.Reason.Should().Be(PromoteClassifier.OtherFileReason);
    }

    [Fact]
    public void LiveRowChangedAfterImport_IsConflict()
    {
        var decisions = Classify(
            new[] { Draft(title: "NEW TITLE") },
            new[] { Live(updatedAt: ImportedAt.AddMinutes(5)) });

        var decision = decisions.Should().ContainSingle().Subject;
        decision.Action.Should().Be(PromoteAction.Conflict);
        decision.Reason.Should().Be(PromoteClassifier.ChangedSinceImportReason);
    }

    // (ProjectId, DocumentNumber) is unique on Live, so a duplicate can never be written.
    [Fact]
    public void DuplicateWithinTheFile_IsConflict()
    {
        var decisions = Classify(
            new[] { Draft(), Draft(isDuplicate: true) },
            Array.Empty<PromoteLiveRow>());

        decisions.Should().HaveCount(2);
        decisions[0].Action.Should().Be(PromoteAction.Add);
        decisions[1].Action.Should().Be(PromoteAction.Conflict);
        decisions[1].Reason.Should().Be(PromoteClassifier.DuplicateReason);
    }

    [Fact]
    public void LiveRowOfThisFileMissingFromDraft_IsDeleted()
    {
        var orphan = Live(number: "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0099");

        var decisions = Classify(new[] { Draft() }, new[] { Live(), orphan });

        decisions.Should().Contain(d => d.Action == PromoteAction.Delete
            && d.DocumentNumber == orphan.DocumentNumber
            && d.LiveId == orphan.Id);
    }

    // Only rows this file owns are deletion candidates — another file's rows are not ours to remove.
    [Fact]
    public void LiveRowOfAnotherFileMissingFromDraft_IsNotDeleted()
    {
        var foreign = Live(
            number: "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0099", folderFileId: OtherFile);

        var decisions = Classify(new[] { Draft() }, new[] { Live(), foreign });

        decisions.Should().NotContain(d => d.Action == PromoteAction.Delete);
    }

    [Fact]
    public void LiveRowWithNoOriginFile_IsAdoptedNotConflicted()
    {
        var decisions = Classify(
            new[] { Draft(title: "NEW TITLE") },
            new[] { Live(folderFileId: null) with { FolderFileId = null } });

        decisions.Should().ContainSingle().Which.Action.Should().Be(PromoteAction.Update);
    }
}
