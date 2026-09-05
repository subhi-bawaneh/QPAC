using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Phase 4.1 end-to-end against real Neon:
//   1. Upload samples/TIDP-STL.xlsx into a Draft-target folder and import it.
//   2. Page the draft rows.
//   3. Edit one row's Sequence -> DocumentNumber is regenerated from F01..F08C.
//   4. Edit a row to collide with an existing number -> the later row is flagged
//      IsDuplicate, the first is not.
//   5. Bulk-set a field across several rows.
[Collection(IntegrationTestCollection.Name)]
public class DraftsFlowTests
{
    private static readonly Guid QpacProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string FirstDocumentNumber = "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004";

    private readonly DipApiFactory _factory;

    public DraftsFlowTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task List_Edit_Duplicate_BulkUpdate_EndToEnd()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await AuthedAdminAsync();
        var folderFileId = await ImportTidpAsDraftAsync(admin);

        // ---- 2. Paged listing.
        var firstPage = await GetJsonAsync(admin,
            $"/api/drafts/documents?folderFileId={folderFileId}&page=1&pageSize=25");
        var total = firstPage.GetProperty("total").GetInt32();
        total.Should().BeGreaterThan(50, "the TIDP-STL sample has hundreds of rows");
        firstPage.GetProperty("items").GetArrayLength().Should().Be(25);
        firstPage.GetProperty("pageSize").GetInt32().Should().Be(25);

        var secondPage = await GetJsonAsync(admin,
            $"/api/drafts/documents?folderFileId={folderFileId}&page=2&pageSize=25");
        var firstIds = Ids(firstPage);
        var secondIds = Ids(secondPage);
        secondIds.Should().NotIntersectWith(firstIds, "paging must not repeat rows");

        // Every row is New on an empty Live layer, and none is a duplicate.
        var newOnly = await GetJsonAsync(admin,
            $"/api/drafts/documents?folderFileId={folderFileId}&state=New&pageSize=1");
        newOnly.GetProperty("total").GetInt32().Should().Be(total);
        var duplicates = await GetJsonAsync(admin,
            $"/api/drafts/documents?folderFileId={folderFileId}&isDuplicate=true&pageSize=1");
        duplicates.GetProperty("total").GetInt32().Should().Be(0);

        // ---- 3. Edit the Sequence of a known row; the number must follow.
        // 9998 is outside the sample's sequence range, so the edit cannot collide.
        var target = await FindByNumberAsync(admin, folderFileId, FirstDocumentNumber);
        var edited = await PutRowAsync(admin, target, ("f08CSequence", "9998"), ("title", "EDITED TITLE"));

        edited.GetProperty("documentNumber").GetString()
            .Should().Be("QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ9998",
                "the number is recomposed from F01..F08C after the edit");
        edited.GetProperty("f08CSequence").GetString().Should().Be("9998");
        edited.GetProperty("title").GetString().Should().Be("EDITED TITLE");
        edited.GetProperty("isDuplicate").GetBoolean().Should().BeFalse();
        edited.GetProperty("state").GetString().Should().Be("New");

        var stillNoDuplicates = await GetJsonAsync(admin,
            $"/api/drafts/documents?folderFileId={folderFileId}&isDuplicate=true&pageSize=1");
        stillNoDuplicates.GetProperty("total").GetInt32().Should().Be(0,
            "9998 was unused, so renumbering that row collides with nothing");

        // ---- 4. Point a second row at an existing number -> duplicate.
        var neighbour = await FindOtherRowAsync(admin, folderFileId, edited.GetProperty("id").GetGuid());
        var collided = await PutRowAsync(admin, neighbour,
            ("f01Project", edited.GetProperty("f01Project").GetString()!),
            ("f02Originator", edited.GetProperty("f02Originator").GetString()!),
            ("f03Contract", edited.GetProperty("f03Contract").GetString()!),
            ("f04DocType", edited.GetProperty("f04DocType").GetString()!),
            ("f05Discipline", edited.GetProperty("f05Discipline").GetString()!),
            ("f06Zone", edited.GetProperty("f06Zone").GetString()!),
            ("f07Building", edited.GetProperty("f07Building").GetString()!),
            ("f08ADrawingType", edited.GetProperty("f08ADrawingType").GetString()!),
            ("f08BLevel", edited.GetProperty("f08BLevel").GetString()!),
            ("f08CSequence", "9998"));

        collided.GetProperty("documentNumber").GetString()
            .Should().Be("QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ9998");

        var duplicatesAfter = await GetJsonAsync(admin,
            $"/api/drafts/documents?folderFileId={folderFileId}&isDuplicate=true&pageSize=50");
        duplicatesAfter.GetProperty("total").GetInt32().Should().Be(1,
            "only the later of the two colliding rows is flagged");
        Ids(duplicatesAfter).Should().ContainSingle()
            .Which.Should().Be(collided.GetProperty("id").GetGuid());

        // ---- 5. Bulk update.
        var bulkIds = Ids(firstPage).Take(5).ToArray();
        var bulkResp = await admin.PutAsJsonAsync("/api/drafts/documents/bulk", new
        {
            ids = bulkIds,
            fields = new { packageName = "PKG-BULK", scopeArea = "ZONE-B" },
        });
        bulkResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await bulkResp.Content.ReadFromJsonAsync<int>()).Should().Be(bulkIds.Length);

        var afterBulk = await GetJsonAsync(admin,
            $"/api/drafts/documents?folderFileId={folderFileId}&search=PKG-BULK&pageSize=50");
        afterBulk.GetProperty("total").GetInt32().Should().Be(0, "search only matches number and title");

        var allRows = await GetAllRowsAsync(admin, folderFileId);
        var touched = allRows.Where(i => bulkIds.Contains(i.GetProperty("id").GetGuid())).ToList();
        touched.Should().HaveCount(bulkIds.Length);
        touched.Should().OnlyContain(i => i.GetProperty("packageName").GetString() == "PKG-BULK");
        touched.Should().OnlyContain(i => i.GetProperty("scopeArea").GetString() == "ZONE-B");
    }

    [Fact]
    public async Task BulkUpdate_WithNoFields_IsRejected()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await AuthedAdminAsync();
        var response = await admin.PutAsJsonAsync("/api/drafts/documents/bulk", new
        {
            ids = new[] { Guid.NewGuid() },
            fields = new { },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateDraftDocument_UnknownId_Returns404()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await AuthedAdminAsync();
        var response = await admin.PutAsJsonAsync($"/api/drafts/documents/{Guid.NewGuid()}", new
        {
            title = "X",
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

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Guid> ImportTidpAsDraftAsync(HttpClient admin)
    {
        var folderResp = await admin.PostAsJsonAsync("/api/folders", new
        {
            projectId = QpacProjectId,
            parentId = (Guid?)null,
            name = $"DraftsE2E-{Guid.NewGuid():N}",
        });
        folderResp.EnsureSuccessStatusCode();
        var folderId = Guid.Parse((await folderResp.Content.ReadAsStringAsync()).Trim('"'));

        var targetResp = await admin.PutAsJsonAsync($"/api/folders/{folderId}/target", new { target = "Draft" });
        targetResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var samplePath = ResolveSamplePath("TIDP-STL.xlsx");
        var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(samplePath));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        multipart.Add(fileContent, "file", "TIDP-STL.xlsx");
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

        var stepResp = await admin.PostAsync($"/api/imports/{batchId}/step", content: null);
        stepResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await stepResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("done").GetBoolean().Should().BeTrue();

        return folderFileId;
    }

    // Pages through every draft row of the file — the sample runs to several
    // hundred rows, so a single page is not guaranteed to hold them all.
    private async Task<List<JsonElement>> GetAllRowsAsync(HttpClient admin, Guid folderFileId)
    {
        const int pageSize = 200;
        var rows = new List<JsonElement>();
        var page = 1;
        while (true)
        {
            var body = await GetJsonAsync(admin,
                $"/api/drafts/documents?folderFileId={folderFileId}&page={page}&pageSize={pageSize}");
            rows.AddRange(body.GetProperty("items").EnumerateArray());
            if (rows.Count >= body.GetProperty("total").GetInt32()) return rows;
            page++;
        }
    }

    private async Task<JsonElement> FindByNumberAsync(HttpClient admin, Guid folderFileId, string number)
    {
        var page = await GetJsonAsync(admin,
            $"/api/drafts/documents?folderFileId={folderFileId}&search={Uri.EscapeDataString(number)}");
        page.GetProperty("total").GetInt32().Should().Be(1);
        return page.GetProperty("items")[0];
    }

    private async Task<JsonElement> FindOtherRowAsync(HttpClient admin, Guid folderFileId, Guid excludeId)
    {
        var page = await GetJsonAsync(admin, $"/api/drafts/documents?folderFileId={folderFileId}&pageSize=10");
        return page.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("id").GetGuid() != excludeId);
    }

    // PUT the row back unchanged except for the named overrides — the endpoint is a
    // full replace, so the payload is built from the row as it currently stands.
    private async Task<JsonElement> PutRowAsync(
        HttpClient admin, JsonElement row, params (string Field, string Value)[] overrides)
    {
        var payload = new Dictionary<string, object?>
        {
            ["title"] = row.GetProperty("title").GetString(),
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
        foreach (var (field, value) in overrides) payload[field] = value;

        var response = await admin.PutAsJsonAsync(
            $"/api/drafts/documents/{row.GetProperty("id").GetGuid()}", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string? Str(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static Guid[] Ids(JsonElement page) => page.GetProperty("items").EnumerateArray()
        .Select(i => i.GetProperty("id").GetGuid()).ToArray();

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
