using System.Net.Http.Headers;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Phase 5.6 end-to-end against real Postgres: import a TIDP Live, recalculate in
// chunks, then read every report endpoint and export one to xlsx.
//
// The numbers themselves are verified against Tracker.xlsx in the engine tests; this
// checks the wiring — snapshots are written, the reports read them, and the totals
// agree with the documents that were imported.
[Collection(IntegrationTestCollection.Name)]
public class ReportsFlowTests
{
    private static readonly Guid QpacProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly DipApiFactory _factory;

    public ReportsFlowTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Import_Recalculate_ThenEveryReportReadsTheSnapshots()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await AuthedAdminAsync();
        var imported = await ImportTidpLiveAsync(admin, "TIDP-STL-AFCO.xlsx");
        imported.Should().BeGreaterThan(0);

        // ---- the reports always answer and report whether anything is unmaterialised.
        var afterImport = await GetJsonAsync(admin, $"/api/projects/{QpacProjectId}/summaries/corporate");
        afterImport.GetProperty("documentsWithoutSnapshot").ValueKind
            .Should().Be(JsonValueKind.Number);

        // ---- the step endpoint stays as the admin fallback and is idempotent, even
        // though the import worker has already recalculated.
        var steps = 0;
        var offset = 0;
        var processed = 0;
        while (steps++ < 100)
        {
            var response = await admin.PostAsJsonAsync(
                $"/api/projects/{QpacProjectId}/recalculate/step", new { offset, take = 250 });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            processed += body.GetProperty("processed").GetInt32();
            offset = body.GetProperty("nextOffset").GetInt32();
            if (body.GetProperty("done").GetBoolean()) break;
        }

        steps.Should().BeLessThan(100, "the recalculation must terminate");
        processed.Should().BeGreaterThanOrEqualTo(imported);

        var corporate = await GetJsonAsync(admin, $"/api/projects/{QpacProjectId}/summaries/corporate");
        corporate.GetProperty("recalculationRequired").GetBoolean().Should().BeFalse(
            "every document now has a snapshot");

        var summary = corporate.GetProperty("summary");
        var total = summary.GetProperty("total");
        total.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(imported);
        summary.GetProperty("disciplines").GetArrayLength().Should().BeGreaterThan(0);
        summary.GetProperty("weeks").GetArrayLength().Should().BeGreaterThan(0);

        // ---- tracker paging
        var firstPage = await GetJsonAsync(admin, $"/api/projects/{QpacProjectId}/tracker?page=1&pageSize=10");
        firstPage.GetProperty("total").GetInt32().Should().Be(total.GetProperty("total").GetInt32());
        firstPage.GetProperty("items").GetArrayLength().Should().Be(10);

        var documentId = firstPage.GetProperty("items")[0].GetProperty("documentId").GetGuid();
        var detail = await GetJsonAsync(admin, $"/api/tracker/documents/{documentId}");
        detail.GetProperty("row").GetProperty("documentNumber").GetString().Should().NotBeNullOrEmpty();
        detail.TryGetProperty("revisions", out _).Should().BeTrue();

        // Filters narrow the same list.
        var discipline = firstPage.GetProperty("items")[0].GetProperty("discipline").GetString();
        var filtered = await GetJsonAsync(admin,
            $"/api/projects/{QpacProjectId}/tracker?discipline={Uri.EscapeDataString(discipline!)}&pageSize=1");
        filtered.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
        filtered.GetProperty("total").GetInt32().Should().BeLessThanOrEqualTo(firstPage.GetProperty("total").GetInt32());

        // ---- the other three reports
        var baseline = await GetJsonAsync(admin, $"/api/projects/{QpacProjectId}/summaries/baseline");
        baseline.GetProperty("summary").GetProperty("total").GetProperty("total").GetInt32()
            .Should().BeGreaterThanOrEqualTo(imported);

        var evm = await GetJsonAsync(admin, $"/api/projects/{QpacProjectId}/summaries/evm");
        evm.GetProperty("summary").GetProperty("total").GetProperty("budgetAtCompletion").GetDecimal()
            .Should().BeGreaterThan(0);

        var findings = await GetJsonAsync(admin, $"/api/projects/{QpacProjectId}/control-findings");
        findings.GetProperty("findings").TryGetProperty("unplanned", out var unplanned).Should().BeTrue();
        unplanned.GetArrayLength().Should().BeGreaterThan(0,
            "nothing has a baseline in this project, so every document is unplanned");

        // ---- export
        var export = await admin.GetAsync($"/api/projects/{QpacProjectId}/reports/Tracker/export");
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        export.Content.Headers.ContentType?.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var bytes = await export.Content.ReadAsByteArrayAsync();
        bytes.Should().HaveCountGreaterThan(1000);
        // xlsx is a zip: "PK".
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
    }

    // RoleDefinitions gives Viewer reports.view AND reports.export — a viewer is meant
    // to be able to take a report away with them. What they cannot do is rebuild the
    // snapshots, which is an import.run operation.
    [Fact]
    public async Task Viewer_CanReadAndExportReports_ButNotRecalculate()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await AuthedAdminAsync();
        var viewer = await AuthedViewerAsync(admin);

        var read = await viewer.GetAsync($"/api/projects/{QpacProjectId}/summaries/corporate");
        read.StatusCode.Should().Be(HttpStatusCode.OK, "reports.view is enough to read a summary");

        var recalculate = await viewer.PostAsJsonAsync(
            $"/api/projects/{QpacProjectId}/recalculate/step", new { offset = 0, take = 10 });
        recalculate.StatusCode.Should().Be(HttpStatusCode.Forbidden, "recalculation needs import.run");

        var export = await viewer.GetAsync($"/api/projects/{QpacProjectId}/reports/Tracker/export");
        export.StatusCode.Should().Be(HttpStatusCode.OK, "the Viewer role is seeded with reports.export");

        var anonymous = _factory.CreateClient();
        var unauthenticated = await anonymous.GetAsync(
            $"/api/projects/{QpacProjectId}/reports/Tracker/export");
        unauthenticated.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<int> ImportTidpLiveAsync(HttpClient admin, string sampleFile)
    {
        var folderResp = await admin.PostAsJsonAsync("/api/folders", new
        {
            projectId = QpacProjectId,
            parentId = (Guid?)null,
            name = $"ReportsE2E-{Guid.NewGuid():N}",
        });
        folderResp.EnsureSuccessStatusCode();
        var folderId = Guid.Parse((await folderResp.Content.ReadAsStringAsync()).Trim('"'));

        var file = await TestHelpers.ImportedAsync(admin, folderId, sampleFile);
        var batches = await GetJsonAsync(
            admin, $"/api/projects/{QpacProjectId}/imports?kind=Tidp&take=20");
        var batch = batches.EnumerateArray()
            .First(b => b.GetProperty("folderFileId").GetGuid() == file.GetProperty("id").GetGuid());
        return batch.GetProperty("rowsRead").GetInt32();
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "GET {0} should succeed", url);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<HttpClient> AuthedAdminAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = DipApiFactory.SuperAdminEmail,
            password = DipApiFactory.SuperAdminPassword,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<HttpClient> AuthedViewerAsync(HttpClient admin)
    {
        var email = $"viewer-reports-{Guid.NewGuid():N}@dip.test";
        const string password = "ViewerPassw0rd!";
        var created = await admin.PostAsJsonAsync("/api/users", new
        {
            email,
            password,
            fullName = "Reports Viewer",
            roles = new[] { "Viewer" },
        });
        created.EnsureSuccessStatusCode();

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("accessToken").GetString());
        return client;
    }

}
