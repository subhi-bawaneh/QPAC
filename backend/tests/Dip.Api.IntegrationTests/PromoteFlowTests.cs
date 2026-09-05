using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Phase 4.2, the four scenarios of PLAN.md § 9 (المرحلة 4 / 4.2), against real Postgres:
//   (a) Draft import then Promote on an empty Live layer -> every row Added.
//   (b) Edit one draft row, promote again -> exactly one Modified, with AuditLog rows.
//   (c) A number already owned by another file -> Conflict, and Live is left untouched.
//   (d) Rollback restores the pre-promote state.
[Collection(IntegrationTestCollection.Name)]
public class PromoteFlowTests
{
    private static readonly Guid QpacProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly DipApiFactory _factory;

    public PromoteFlowTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Promote_OnEmptyLive_AddsEveryRow_ThenEdit_PromotesOneModified()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await AuthedAdminAsync();
        var file = await ImportTidpAsDraftAsync(admin, "TIDP-STL.xlsx");

        // ---- (a) diff before writing anything.
        var diff = await GetJsonAsync(admin, $"/api/drafts/promote-diff?folderFileId={file.FolderFileId}");
        var draftCount = await DraftCountAsync(admin, file.FolderFileId);
        diff.GetProperty("added").GetInt32().Should().Be(draftCount, "Live is empty, so every draft row is new");
        diff.GetProperty("modified").GetInt32().Should().Be(0);
        diff.GetProperty("deleted").GetInt32().Should().Be(0);
        diff.GetProperty("conflicts").GetInt32().Should().Be(0);
        diff.GetProperty("addedRows").GetArrayLength().Should().BeLessThanOrEqualTo(200, "row lists are capped");

        var promote = await PromoteAsync(admin, file.FolderFileId);
        promote.GetProperty("added").GetInt32().Should().Be(draftCount);
        promote.GetProperty("updated").GetInt32().Should().Be(0);
        promote.GetProperty("conflicts").GetInt32().Should().Be(0);
        promote.GetProperty("recalculationRequired").GetBoolean().Should().BeTrue();

        // Drafts are marked Unchanged and a second diff is a no-op.
        var afterDiff = await GetJsonAsync(admin, $"/api/drafts/promote-diff?folderFileId={file.FolderFileId}");
        afterDiff.GetProperty("added").GetInt32().Should().Be(0);
        afterDiff.GetProperty("modified").GetInt32().Should().Be(0);
        afterDiff.GetProperty("conflicts").GetInt32().Should().Be(0,
            "a file's own promote must not make its rows look externally modified");
        afterDiff.GetProperty("unchanged").GetInt32().Should().Be(draftCount);

        // ---- (b) edit one row, then promote again.
        var row = await FirstDraftRowAsync(admin, file.FolderFileId);
        var edited = await EditTitleAsync(admin, row, "PROMOTE TEST TITLE");
        edited.GetProperty("state").GetString().Should().Be("Modified");

        var secondDiff = await GetJsonAsync(admin, $"/api/drafts/promote-diff?folderFileId={file.FolderFileId}");
        secondDiff.GetProperty("modified").GetInt32().Should().Be(1);
        secondDiff.GetProperty("added").GetInt32().Should().Be(0);
        var change = secondDiff.GetProperty("modifiedRows")[0].GetProperty("changes")[0];
        change.GetProperty("field").GetString().Should().Be("Title");
        change.GetProperty("newValue").GetString().Should().Be("PROMOTE TEST TITLE");

        var secondPromote = await PromoteAsync(admin, file.FolderFileId);
        secondPromote.GetProperty("updated").GetInt32().Should().Be(1);
        secondPromote.GetProperty("added").GetInt32().Should().Be(0);

        // ---- (d) rollback puts the old title back.
        var rollback = await admin.PostAsync(
            $"/api/drafts/promote/{secondPromote.GetProperty("promoteBatchId").GetGuid()}/rollback", null);
        rollback.StatusCode.Should().Be(HttpStatusCode.OK,
            "rollback failed: {0}", await rollback.Content.ReadAsStringAsync());
        var rollbackBody = await rollback.Content.ReadFromJsonAsync<JsonElement>();
        rollbackBody.GetProperty("restored").GetInt32().Should().Be(1);
        rollbackBody.GetProperty("removed").GetInt32().Should().Be(0);

        // The edit is pending again: the draft still says "PROMOTE TEST TITLE",
        // Live is back to the original, so the diff shows one Modified row once more.
        var afterRollback = await GetJsonAsync(admin, $"/api/drafts/promote-diff?folderFileId={file.FolderFileId}");
        afterRollback.GetProperty("modified").GetInt32().Should().Be(1);
        afterRollback.GetProperty("modifiedRows")[0].GetProperty("changes")[0]
            .GetProperty("oldValue").GetString().Should().Be(row.GetProperty("title").GetString());

        // A batch cannot be rolled back twice.
        var again = await admin.PostAsync(
            $"/api/drafts/promote/{secondPromote.GetProperty("promoteBatchId").GetGuid()}/rollback", null);
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rollback_OfTheFirstPromote_RemovesEveryRowItAdded()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await AuthedAdminAsync();
        var file = await ImportTidpAsDraftAsync(admin, "TIDP-STL-AFCO.xlsx");
        var draftCount = await DraftCountAsync(admin, file.FolderFileId);

        var promote = await PromoteAsync(admin, file.FolderFileId);
        promote.GetProperty("added").GetInt32().Should().Be(draftCount);

        var rollback = await admin.PostAsync(
            $"/api/drafts/promote/{promote.GetProperty("promoteBatchId").GetGuid()}/rollback", null);
        rollback.StatusCode.Should().Be(HttpStatusCode.OK);
        (await rollback.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("removed").GetInt32().Should().Be(draftCount);

        // Live is empty again, so every draft row is New (and Added) once more.
        var diff = await GetJsonAsync(admin, $"/api/drafts/promote-diff?folderFileId={file.FolderFileId}");
        diff.GetProperty("added").GetInt32().Should().Be(draftCount);
        diff.GetProperty("unchanged").GetInt32().Should().Be(0);
    }

    // ---- (c) the same numbers arriving from a second file are conflicts, and the
    // Live rows written by the first file are left exactly as they were.
    [Fact]
    public async Task NumbersOwnedByAnotherFile_AreConflicts_AndLiveIsUntouched()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await AuthedAdminAsync();

        var first = await ImportTidpAsDraftAsync(admin, "TIDP-STL-AFCO.xlsx");
        await PromoteAsync(admin, first.FolderFileId);
        var firstRow = await FirstDraftRowAsync(admin, first.FolderFileId);

        // The very same workbook uploaded as a second file: identical numbers,
        // different origin -> every row conflicts.
        var second = await ImportTidpAsDraftAsync(admin, "TIDP-STL-AFCO.xlsx");
        var secondCount = await DraftCountAsync(admin, second.FolderFileId);

        var diff = await GetJsonAsync(admin, $"/api/drafts/promote-diff?folderFileId={second.FolderFileId}");
        diff.GetProperty("conflicts").GetInt32().Should().Be(secondCount);
        diff.GetProperty("added").GetInt32().Should().Be(0);
        diff.GetProperty("conflictRows")[0].GetProperty("reason").GetString()
            .Should().Be("A Live document with this number came from another file");

        var promote = await PromoteAsync(admin, second.FolderFileId);
        promote.GetProperty("conflicts").GetInt32().Should().Be(secondCount);
        promote.GetProperty("added").GetInt32().Should().Be(0);
        promote.GetProperty("updated").GetInt32().Should().Be(0);
        promote.GetProperty("recalculationRequired").GetBoolean().Should().BeFalse(
            "nothing was written, so no snapshot is stale");

        // The conflicted rows are marked in the draft for the reviewer.
        var conflicted = await GetJsonAsync(admin,
            $"/api/drafts/documents?folderFileId={second.FolderFileId}&state=Conflict&pageSize=1");
        conflicted.GetProperty("total").GetInt32().Should().Be(secondCount);

        // And the first file's promote still owns Live, unchanged.
        var firstDiff = await GetJsonAsync(admin, $"/api/drafts/promote-diff?folderFileId={first.FolderFileId}");
        firstDiff.GetProperty("unchanged").GetInt32().Should().Be(
            await DraftCountAsync(admin, first.FolderFileId));
        firstDiff.GetProperty("conflicts").GetInt32().Should().Be(0);
        firstRow.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PromoteDiff_ForAFileWithNoDraft_Returns404()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await AuthedAdminAsync();
        var response = await admin.GetAsync($"/api/drafts/promote-diff?folderFileId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Viewer_IsForbidden_FromPromoting()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await AuthedAdminAsync();
        var viewerEmail = $"viewer-promote-{Guid.NewGuid():N}@dip.test";
        const string viewerPassword = "ViewerPassw0rd!";
        var createUser = await admin.PostAsJsonAsync("/api/users", new
        {
            email = viewerEmail,
            password = viewerPassword,
            fullName = "Promote Viewer",
            roles = new[] { "Viewer" },
        });
        createUser.EnsureSuccessStatusCode();

        var viewer = _factory.CreateClient();
        var login = await viewer.PostAsJsonAsync("/api/auth/login", new { email = viewerEmail, password = viewerPassword });
        login.EnsureSuccessStatusCode();
        viewer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());

        var response = await viewer.PostAsJsonAsync("/api/drafts/promote", new
        {
            folderFileId = Guid.NewGuid(),
            deleteMissing = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ helpers

    private sealed record ImportedFile(Guid FolderId, Guid FolderFileId);

    private async Task<ImportedFile> ImportTidpAsDraftAsync(HttpClient admin, string sampleFile)
    {
        var folderResp = await admin.PostAsJsonAsync("/api/folders", new
        {
            projectId = QpacProjectId,
            parentId = (Guid?)null,
            name = $"PromoteE2E-{Guid.NewGuid():N}",
        });
        folderResp.EnsureSuccessStatusCode();
        var folderId = Guid.Parse((await folderResp.Content.ReadAsStringAsync()).Trim('"'));

        (await admin.PutAsJsonAsync($"/api/folders/{folderId}/target", new { target = "Draft" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var multipart = new MultipartFormDataContent();
        var content = new ByteArrayContent(await File.ReadAllBytesAsync(ResolveSamplePath(sampleFile)));
        content.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        multipart.Add(content, "file", sampleFile);
        (await admin.PostAsync($"/api/folders/{folderId}/files", multipart)).EnsureSuccessStatusCode();

        var detail = await GetJsonAsync(admin, $"/api/folders/{folderId}");
        var folderFileId = detail.GetProperty("files")[0].GetProperty("id").GetGuid();

        var startResp = await admin.PostAsJsonAsync("/api/imports/start", new
        {
            projectId = QpacProjectId,
            folderFileId,
            kind = "Tidp",
            target = "Draft",
        });
        startResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var batchId = (await startResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("importBatchId").GetGuid();

        var stepResp = await admin.PostAsync($"/api/imports/{batchId}/step", null);
        stepResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await stepResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("done").GetBoolean().Should().BeTrue();

        return new ImportedFile(folderId, folderFileId);
    }

    private async Task<JsonElement> PromoteAsync(HttpClient admin, Guid folderFileId, bool deleteMissing = false)
    {
        var response = await admin.PostAsJsonAsync("/api/drafts/promote", new { folderFileId, deleteMissing });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<int> DraftCountAsync(HttpClient admin, Guid folderFileId)
    {
        var page = await GetJsonAsync(admin, $"/api/drafts/documents?folderFileId={folderFileId}&pageSize=1");
        return page.GetProperty("total").GetInt32();
    }

    private async Task<JsonElement> FirstDraftRowAsync(HttpClient admin, Guid folderFileId)
    {
        var page = await GetJsonAsync(admin, $"/api/drafts/documents?folderFileId={folderFileId}&pageSize=1");
        return page.GetProperty("items")[0];
    }

    private async Task<JsonElement> EditTitleAsync(HttpClient admin, JsonElement row, string title)
    {
        var payload = new Dictionary<string, object?>
        {
            ["title"] = title,
            ["extractedFromModel"] = Str(row, "extractedFromModel"),
            ["scopeArea"] = Str(row, "scopeArea"),
            ["authoringSoftware"] = Str(row, "authoringSoftware"),
            ["exchangeFormat"] = Str(row, "exchangeFormat"),
            ["scale"] = Str(row, "scale"),
            ["deliveryMilestone"] = Str(row, "deliveryMilestone"),
            ["packageName"] = Str(row, "packageName"),
            ["activityId"] = Str(row, "activityId"),
            ["classificationCode"] = Str(row, "classificationCode"),
            ["f01Project"] = row.GetProperty("f01Project").GetString(),
            ["f02Originator"] = row.GetProperty("f02Originator").GetString(),
            ["f03Contract"] = row.GetProperty("f03Contract").GetString(),
            ["f04DocType"] = row.GetProperty("f04DocType").GetString(),
            ["f05Discipline"] = row.GetProperty("f05Discipline").GetString(),
            ["f06Zone"] = row.GetProperty("f06Zone").GetString(),
            ["f07Building"] = row.GetProperty("f07Building").GetString(),
            ["f08ADrawingType"] = row.GetProperty("f08ADrawingType").GetString(),
            ["f08BLevel"] = row.GetProperty("f08BLevel").GetString(),
            ["f08CSequence"] = row.GetProperty("f08CSequence").GetString(),
            ["corporateDiscipline"] = row.GetProperty("corporateDiscipline").GetString(),
        };

        var response = await admin.PutAsJsonAsync(
            $"/api/drafts/documents/{row.GetProperty("id").GetGuid()}", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string? Str(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

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

    private static string ResolveSamplePath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", fileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Cannot locate samples/{fileName}");
    }
}
