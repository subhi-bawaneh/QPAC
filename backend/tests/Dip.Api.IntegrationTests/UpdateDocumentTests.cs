using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Editing a Live row: the number is recomposed from the eight fields, every changed
// field lands in AuditLog, and a number another row already holds is a 409.
[Collection(IntegrationTestCollection.Name)]
public class UpdateDocumentTests
{
    private readonly DipApiFactory _factory;

    public UpdateDocumentTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ChangingTheSequence_RecomposesTheNumber_AndWritesAudit()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var world = await ImportLiveAsync(admin);
        var (row, nextSequence) = await RenumberableRowAsync(world.ProjectId);

        var payload = Payload(row);
        payload["f08CSequence"] = nextSequence;
        payload["title"] = "EDITED LIVE TITLE";

        var response = await admin.PutAsJsonAsync($"/api/documents/{row.Id}", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "update failed: {0}", await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("f08CSequence").GetString().Should().Be(nextSequence, "leading zeros survive");
        body.GetProperty("documentNumber").GetString().Should()
            .Be(row.DocumentNumber[..^nextSequence.Length] + nextSequence);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var audits = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityId == row.Id && a.Action == "Update")
            .Select(a => a.Field)
            .ToListAsync();
        audits.Should().Contain("Title");
        audits.Should().Contain("F08CSequence");
        audits.Should().Contain("DocumentNumber");
        audits.Should().NotContain("Scale", "unchanged fields are not audited");

        (await db.DocumentSnapshots.AnyAsync(s => s.DocumentId == row.Id))
            .Should().BeFalse("the row's computed columns are dropped until the engine reruns");
    }

    [Fact]
    public async Task ANumberAnotherRowHolds_Is409()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var world = await ImportLiveAsync(admin);
        var first = await FirstDocumentAsync(world.ProjectId, "0004");
        var second = await FirstDocumentAsync(world.ProjectId, "0005");

        var payload = Payload(first);
        payload["f08CSequence"] = second.F08CSequence;
        payload["f08ADrawingType"] = second.F08ADrawingType;
        payload["f08BLevel"] = second.F08BLevel;
        payload["f07Building"] = second.F07Building;
        payload["f06Zone"] = second.F06Zone;
        payload["f04DocType"] = second.F04DocType;
        payload["f05Discipline"] = second.F05Discipline;

        var response = await admin.PutAsJsonAsync($"/api/documents/{first.Id}", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Viewer_IsForbidden()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var viewer = await TestHelpers.AuthedAsync(_factory, admin, "Viewer", "document-viewer");

        var response = await viewer.PutAsJsonAsync($"/api/documents/{Guid.NewGuid()}", new
        {
            title = "x",
            f01Project = "QF01012",
            f02Originator = "NES",
            f03Contract = "C04518",
            f04DocType = "SDW",
            f05Discipline = "STL",
            f06Zone = "00",
            f07Building = "Z00000",
            f08ADrawingType = "0",
            f08BLevel = "ZZ",
            f08CSequence = "0001",
            corporateDiscipline = "Structural",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ helpers

    private sealed record World(Guid ProjectId, Guid FolderId);

    private sealed record DocumentRow(
        Guid Id, string DocumentNumber, string Title, string F04DocType, string F05Discipline,
        string F06Zone, string F07Building, string F08ADrawingType, string F08BLevel,
        string F08CSequence, string CorporateDiscipline, string F01Project, string F02Originator,
        string F03Contract, string? ExtractedFromModel, string? ScopeArea, string? AuthoringSoftware,
        string? ExchangeFormat, string? Scale, DateTime? DeliveryMilestone, string? PackageName,
        string? ActivityId, string? ClassificationCode);

    private async Task<World> ImportLiveAsync(HttpClient admin)
    {
        // The sample workbook's rows carry PROJECT = QF01012, and the importer rejects a
        // row whose project code is not the project's own, so the seeded QPAC project is
        // the only one it can be imported into.
        var projectId = TestHelpers.QpacProjectId;
        var (tidpFileId, _) = await TestHelpers.ImportTidpAsync(_factory, projectId, "TIDP-STL.xlsx");
        return new World(projectId, tidpFileId);
    }

    // Renumbering must land on a free number, so the row is chosen accordingly.
    //
    // The serial width is per document type now (CAL is three digits, SDW four), so
    // the next serial is derived from the row rather than assumed to be "0005".
    private async Task<(DocumentRow Row, string NextSequence)> RenumberableRowAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var documents = await db.Documents.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.DocumentNumber)
            .ToListAsync();
        var taken = documents
            .Select(x => x.DocumentNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var document in documents)
        {
            var sequence = document.F08CSequence;
            if (!int.TryParse(sequence, out var value)) continue;

            var next = (value + 1).ToString().PadLeft(sequence.Length, '0');
            if (next.Length != sequence.Length) continue;   // rolled over a digit

            if (!taken.Contains(document.DocumentNumber[..^sequence.Length] + next))
            {
                return (Map(document), next);
            }
        }

        throw new InvalidOperationException("No document in the sample can be renumbered into a free slot");
    }

    private async Task<DocumentRow> FirstDocumentAsync(Guid projectId, string sequence)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var d = await db.Documents.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.F08CSequence == sequence)
            .OrderBy(x => x.DocumentNumber)
            .FirstAsync();
        return Map(d);
    }

    private static DocumentRow Map(Domain.Entities.Document d) => new(
        d.Id, d.DocumentNumber, d.Title, d.F04DocType, d.F05Discipline, d.F06Zone,
        d.F07Building, d.F08ADrawingType, d.F08BLevel, d.F08CSequence, d.CorporateDiscipline,
        d.F01Project, d.F02Originator, d.F03Contract, d.ExtractedFromModel, d.ScopeArea,
        d.AuthoringSoftware, d.ExchangeFormat, d.Scale, d.DeliveryMilestone, d.PackageName,
        d.ActivityId, d.ClassificationCode);

    private static Dictionary<string, object?> Payload(DocumentRow d) => new()
    {
        ["title"] = d.Title,
        ["f01Project"] = d.F01Project,
        ["f02Originator"] = d.F02Originator,
        ["f03Contract"] = d.F03Contract,
        ["f04DocType"] = d.F04DocType,
        ["f05Discipline"] = d.F05Discipline,
        ["f06Zone"] = d.F06Zone,
        ["f07Building"] = d.F07Building,
        ["f08ADrawingType"] = d.F08ADrawingType,
        ["f08BLevel"] = d.F08BLevel,
        ["f08CSequence"] = d.F08CSequence,
        ["corporateDiscipline"] = d.CorporateDiscipline,
        ["extractedFromModel"] = d.ExtractedFromModel,
        ["scopeArea"] = d.ScopeArea,
        ["authoringSoftware"] = d.AuthoringSoftware,
        ["exchangeFormat"] = d.ExchangeFormat,
        ["scale"] = d.Scale,
        ["deliveryMilestone"] = d.DeliveryMilestone,
        ["packageName"] = d.PackageName,
        ["activityId"] = d.ActivityId,
        ["classificationCode"] = d.ClassificationCode,
    };
}
