using System.Text.Json;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Api.IntegrationTests;

// The point of the effective document set (refactor-plan § 3 R8): a company that still
// works in Google Drive is imported into the Draft layer and one working in the system
// into the Live layer, and every report must read them identically.
//
// The same three workbooks — MIDP (documents + Aconex History) and Baseline — are
// imported into two projects that differ only in their folder target, and the Corporate
// Summary, Baseline Summary and Tracker are compared row for row.
[Collection(IntegrationTestCollection.Name)]
public class DraftLayerReportsTests
{
    private static readonly DateTime ReportDate = new(2026, 8, 30);

    private readonly DipApiFactory _factory;

    public DraftLayerReportsTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DraftAndLiveImports_ProduceIdenticalReports()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var draft = await ImportProjectAsync(admin, "Draft");
        var live = await ImportProjectAsync(admin, "Live");

        var draftSummary = await CorporateSummaryAsync(admin, draft);
        var liveSummary = await CorporateSummaryAsync(admin, live);

        // Anchors, so a comparison of two empty reports cannot pass.
        var draftTotal = draftSummary.GetProperty("total");
        draftTotal.GetProperty("total").GetInt32().Should().BeGreaterThan(15_000);
        draftTotal.GetProperty("submitted").GetInt32().Should().BeGreaterThan(0);
        draftTotal.GetProperty("approved").GetInt32().Should().BeGreaterThan(0);
        draftSummary.GetProperty("disciplines").GetArrayLength().Should().Be(9);
        draftSummary.GetProperty("authors").GetArrayLength().Should().Be(13);

        draftSummary.GetRawText().Should().Be(liveSummary.GetRawText(),
            "the layer a company works in must be invisible to the reports");

        var draftBaseline = await TestHelpers.GetJsonAsync(
            admin, $"/api/projects/{draft}/summaries/baseline");
        var liveBaseline = await TestHelpers.GetJsonAsync(
            admin, $"/api/projects/{live}/summaries/baseline");
        draftBaseline.GetProperty("summary").GetRawText().Should()
            .Be(liveBaseline.GetProperty("summary").GetRawText());

        var draftFindings = await TestHelpers.GetJsonAsync(
            admin, $"/api/projects/{draft}/control-findings");
        var liveFindings = await TestHelpers.GetJsonAsync(
            admin, $"/api/projects/{live}/control-findings");
        draftFindings.GetProperty("findings").GetProperty("unplanned").GetArrayLength()
            .Should().Be(liveFindings.GetProperty("findings").GetProperty("unplanned").GetArrayLength());
        draftFindings.GetProperty("findings").GetProperty("unusedPackages").GetArrayLength()
            .Should().Be(liveFindings.GetProperty("findings").GetProperty("unusedPackages").GetArrayLength());

        // The Tracker pages over snapshots that know which layer they came from.
        var tracker = await TestHelpers.GetJsonAsync(
            admin, $"/api/projects/{draft}/tracker?page=1&pageSize=5");
        tracker.GetProperty("total").GetInt32().Should().Be(draftTotal.GetProperty("total").GetInt32());
        tracker.GetProperty("items")[0].GetProperty("layer").GetString().Should().Be("Draft");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        (await db.Documents.CountAsync(d => d.ProjectId == draft))
            .Should().Be(0, "a Draft-target folder never writes the Live tables");
        (await db.DocumentSnapshots.CountAsync(
                s => s.ProjectId == draft && s.Layer == Domain.Enums.DataTarget.Draft))
            .Should().Be(draftTotal.GetProperty("total").GetInt32());
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Guid> ImportProjectAsync(HttpClient admin, string target)
    {
        var projectId = await TestHelpers.NewProjectAsync(_factory, $"Layer parity {target}");
        await SetReportDateAsync(projectId);

        var folderId = await TestHelpers.CreateFolderAsync(
            admin, $"Parity{target}-{Guid.NewGuid():N}", projectId: projectId);
        if (target == "Draft")
        {
            await TestHelpers.SetTargetAsync(admin, folderId, "Draft");
        }

        // Baseline is always Live (R5) and supplies Planned Start / Finish.
        await ImportAsync(admin, folderId, "Baseline.xlsx", timeoutSeconds: 300);
        // MIDP carries the documents and the Aconex History sheet.
        await ImportAsync(admin, folderId, "MIDP.xlsx", timeoutSeconds: 900);

        await RecalculateAsync(projectId);
        return projectId;
    }

    private static async Task<JsonElement> CorporateSummaryAsync(HttpClient admin, Guid projectId)
    {
        var response = await TestHelpers.GetJsonAsync(
            admin, $"/api/projects/{projectId}/summaries/corporate");
        response.GetProperty("documentsWithoutSnapshot").GetInt32().Should().Be(0);
        return response.GetProperty("summary");
    }

    private static async Task ImportAsync(
        HttpClient admin, Guid folderId, string sampleFile, int timeoutSeconds)
    {
        var fileId = await TestHelpers.UploadSampleAsync(admin, folderId, sampleFile);
        var file = await TestHelpers.WaitForImportAsync(admin, folderId, fileId, timeoutSeconds);
        file.GetProperty("state").GetString().Should().Be("Imported",
            "import error: {0}", file.GetProperty("importError").GetString());
    }

    private async Task SetReportDateAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var project = await db.Projects.FirstAsync(p => p.Id == projectId);
        project.ReportDate = ReportDate;
        await db.SaveChangesAsync();
    }

    private async Task RecalculateAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Features.Recalculation.RecalculationService>()
            .RunAllAsync(projectId, CancellationToken.None);
    }
}
