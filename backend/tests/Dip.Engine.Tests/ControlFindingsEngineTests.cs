using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

public class ControlFindingsEngineTests
{
    private static readonly DateTime Modified = new(2026, 4, 1, 9, 30, 0);

    private static readonly IReadOnlyList<StatusMapping> Statuses = new[]
    {
        new StatusMapping { AconexStatus = "B - Approved with Comments", Status = UnifiedStatus.Approved },
        new StatusMapping { AconexStatus = "C - Revise and Resubmit", Status = UnifiedStatus.Rejected },
    };

    private static Document Doc(
        string number, string? activityId = null, string discipline = "Structural",
        string type = "SDW", string? author = "JINGGONG", string title = "TITLE") => new()
    {
        Id = Guid.NewGuid(),
        DocumentNumber = number,
        Title = title,
        ActivityId = activityId,
        CorporateDiscipline = discipline,
        F04DocType = type,
        Exchanges = new List<DataExchange> { new() { Number = 1, Author = author } },
    };

    private static TrackerRow RowFor(Document document, DateTime? plannedStart) => new(
        document.Id, document.DocumentNumber,
        SubmissionsCount: null, Revision: null, AconexStatus: null, Status: null,
        SubmissionDate: null, DateModified: null, Transmittal: null,
        PlannedStart: plannedStart, PlannedFinish: null, ActualStart: null, ActualFinish: null);

    private static AconexRevision Revision(
        string? docNoFinal, bool isLatest = true, bool inMidp = false,
        string revision = "00", string status = "B - Approved with Comments") => new()
    {
        DocNoFinal = docNoFinal,
        Revision = revision,
        Title = "ACONEX TITLE",
        AconexStatus = status,
        DateModified = Modified,
        IsLatest = isLatest,
        InMidp = inMidp,
    };

    private static BaselineActivity Submittal(string code, string package = "PKG", int duration = 10) => new()
    {
        ActivityCode = code,
        Package = package,
        Type = BaselineActivityType.Submittal,
        OriginalDuration = duration,
        Finish = new DateTime(2026, 6, 30),
    };

    // ---- 1. delivered but unplanned

    [Fact]
    public void Delivered_ListsLatestRevisionsThatAreNotInTheMidp()
    {
        var findings = ControlFindingsEngine.Compute(
            Array.Empty<Document>(), Array.Empty<TrackerRow>(),
            new[]
            {
                Revision("DOC-A"),                                  // reported
                Revision("DOC-B", inMidp: true),                    // planned, so fine
                Revision("DOC-C", isLatest: false),                 // superseded revision
                Revision(null),                                     // not a document number
                Revision(string.Empty),
            },
            Array.Empty<BaselineActivity>(), Statuses);

        findings.DeliveredButUnplanned.Should().ContainSingle()
            .Which.DocumentNumber.Should().Be("DOC-A");
    }

    [Fact]
    public void Delivered_CarriesTheRawStatusAndItsUnifiedMapping()
    {
        var findings = ControlFindingsEngine.Compute(
            Array.Empty<Document>(), Array.Empty<TrackerRow>(),
            new[] { Revision("DOC-A", status: "C - Revise and Resubmit", revision: "02") },
            Array.Empty<BaselineActivity>(), Statuses);

        var row = findings.DeliveredButUnplanned.Single();
        row.Revision.Should().Be("02");
        row.Title.Should().Be("ACONEX TITLE");
        row.AconexStatus.Should().Be("C - Revise and Resubmit");
        row.Status.Should().Be(UnifiedStatus.Rejected);
        row.DateModified.Should().Be(Modified);
    }

    [Fact]
    public void Delivered_UnknownStatus_LeavesTheUnifiedStatusNull()
    {
        var findings = ControlFindingsEngine.Compute(
            Array.Empty<Document>(), Array.Empty<TrackerRow>(),
            new[] { Revision("DOC-A", status: "Something Else") },
            Array.Empty<BaselineActivity>(), Statuses);

        findings.DeliveredButUnplanned.Single().Status.Should().BeNull();
    }

    // ---- 2. unplanned in MIDP

    [Fact]
    public void Unplanned_ListsDocumentsWithNoPlannedStart()
    {
        var planned = Doc("DOC-A");
        var unplanned = Doc("DOC-B", discipline: "Electrical", type: "CAL", author: "AFCO");
        var missingRow = Doc("DOC-C");

        var findings = ControlFindingsEngine.Compute(
            new[] { planned, unplanned, missingRow },
            new[] { RowFor(planned, new DateTime(2026, 1, 1)), RowFor(unplanned, null) },
            Array.Empty<AconexRevision>(), Array.Empty<BaselineActivity>(), Statuses);

        findings.Unplanned.Select(u => u.DocumentNumber).Should().Equal("DOC-B", "DOC-C");

        var row = findings.Unplanned.First();
        row.Type.Should().Be("CAL");
        row.Discipline.Should().Be("Electrical");
        row.Author.Should().Be("AFCO");
        row.PlannedStart.Should().BeNull();
    }

    // ---- 3. unused baseline packages

    [Fact]
    public void UnusedPackages_ListSubmittalActivitiesNoDocumentPointsAt()
    {
        var used = Doc("DOC-A", activityId: "QP.A.1000");

        var findings = ControlFindingsEngine.Compute(
            new[] { used }, new[] { RowFor(used, new DateTime(2026, 1, 1)) },
            Array.Empty<AconexRevision>(),
            new[] { Submittal("QP.A.1000"), Submittal("QP.B.1000", "PKG-B", duration: 25) },
            Statuses);

        var unused = findings.UnusedPackages.Should().ContainSingle().Subject;
        unused.ActivityCode.Should().Be("QP.B.1000");
        unused.Package.Should().Be("PKG-B");
        unused.OriginalDuration.Should().Be(25);
        unused.Finish.Should().Be(new DateTime(2026, 6, 30));
        unused.DocumentCount.Should().Be(0);
    }

    // ---- 4. duplicate document numbers

    [Fact]
    public void Duplicates_ListEveryRowOfADuplicatedNumberWithItsGroupSize()
    {
        var first = Doc("DOC-A", title: "FIRST");
        var second = Doc("DOC-A", title: "SECOND");
        var third = Doc("DOC-A", title: "THIRD");
        var unique = Doc("DOC-B");

        var findings = ControlFindingsEngine.Compute(
            new[] { first, second, third, unique },
            Array.Empty<TrackerRow>(), Array.Empty<AconexRevision>(),
            Array.Empty<BaselineActivity>(), Statuses);

        findings.Duplicates.Should().HaveCount(3, "every row of the group is listed, not just the extras");
        findings.Duplicates.Select(d => d.Title).Should().Equal("FIRST", "SECOND", "THIRD");
        findings.Duplicates.Should().OnlyContain(d => d.Count == 3);
    }

    [Fact]
    public void Duplicates_MatchNumbersCaseInsensitively()
    {
        var findings = ControlFindingsEngine.Compute(
            new[] { Doc("DOC-A"), Doc("doc-a") },
            Array.Empty<TrackerRow>(), Array.Empty<AconexRevision>(),
            Array.Empty<BaselineActivity>(), Statuses);

        findings.Duplicates.Should().HaveCount(2);
    }

    [Fact]
    public void CleanData_ProducesFourEmptyReports()
    {
        var document = Doc("DOC-A", activityId: "QP.A.1000");

        var findings = ControlFindingsEngine.Compute(
            new[] { document },
            new[] { RowFor(document, new DateTime(2026, 1, 1)) },
            new[] { Revision("DOC-A", inMidp: true) },
            new[] { Submittal("QP.A.1000") },
            Statuses);

        findings.DeliveredButUnplanned.Should().BeEmpty();
        findings.Unplanned.Should().BeEmpty();
        findings.UnusedPackages.Should().BeEmpty();
        findings.Duplicates.Should().BeEmpty();
    }
}
