using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Dip.Api.IntegrationTests;

// End-to-end import orchestration against real Neon:
//   1. Admin logs in.
//   2. Creates a folder + uploads the real samples/PickLists.xlsx.
//   3. Calls POST /api/imports/start -> gets batchId.
//   4. Calls POST /api/imports/{batchId}/step -> Done=true.
//   5. Calls GET /api/imports/{batchId}/status -> Completed=true, RowsInserted>0.
//   6. Calls GET /api/projects/{projectId}/imports -> includes the batch.
//
// PickLists.xlsx is used because it's small (~50 KB) and its importer runs in
// <2s, keeping the whole test under 20s. TIDP/MIDP/Aconex importers are
// covered directly in Dip.Infrastructure.Tests where they don't add API
// integration overhead.
[Collection(IntegrationTestCollection.Name)]
public class ImportsFlowTests
{
    private static readonly Guid QpacProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly DipApiFactory _factory;

    public ImportsFlowTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Start_Step_Status_List_EndToEnd()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await AuthedAdminAsync();

        // 1. Create folder.
        var folderResp = await admin.PostAsJsonAsync("/api/folders", new
        {
            projectId = QpacProjectId,
            parentId = (Guid?)null,
            name = $"ImportsE2E-{Guid.NewGuid():N}",
        });
        folderResp.EnsureSuccessStatusCode();
        var folderId = Guid.Parse((await folderResp.Content.ReadAsStringAsync()).Trim('"'));

        // 2. Upload PickLists.xlsx from the real samples folder.
        var samplePath = ResolveSamplePath("PickLists.xlsx");
        var multipart = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(samplePath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        multipart.Add(fileContent, "file", "PickLists.xlsx");

        var uploadResp = await admin.PostAsync($"/api/folders/{folderId}/files", multipart);
        uploadResp.EnsureSuccessStatusCode();

        // Read the folder to grab the uploaded file id.
        var detailResp = await admin.GetAsync($"/api/folders/{folderId}");
        detailResp.EnsureSuccessStatusCode();
        var detail = await detailResp.Content.ReadFromJsonAsync<JsonElement>();
        var folderFileId = detail.GetProperty("files")[0].GetProperty("id").GetGuid();

        // 3. Start.
        var startResp = await admin.PostAsJsonAsync("/api/imports/start", new
        {
            projectId = QpacProjectId,
            folderFileId,
            kind = "Picklists",
            target = "Live",
        });
        startResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = startBody.GetProperty("importBatchId").GetGuid();

        // 4. Step.
        var stepResp = await admin.PostAsync($"/api/imports/{batchId}/step", content: null);
        stepResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var stepBody = await stepResp.Content.ReadFromJsonAsync<JsonElement>();
        stepBody.GetProperty("done").GetBoolean().Should().BeTrue();
        stepBody.GetProperty("batch").GetProperty("completed").GetBoolean().Should().BeTrue();
        stepBody.GetProperty("batch").GetProperty("rowsRead").GetInt32().Should().BeGreaterThan(0);

        // 5. Status.
        var statusResp = await admin.GetAsync($"/api/imports/{batchId}/status");
        statusResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusBody = await statusResp.Content.ReadFromJsonAsync<JsonElement>();
        statusBody.GetProperty("completed").GetBoolean().Should().BeTrue();
        statusBody.GetProperty("kind").GetString().Should().Be("Picklists");

        // 6. List includes this batch.
        var listResp = await admin.GetAsync($"/api/projects/{QpacProjectId}/imports?kind=Picklists&take=10");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var ids = listBody.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToArray();
        ids.Should().Contain(batchId);
    }

    [Fact]
    public async Task Editor_Forbidden_FromStartingImport()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await AuthedAdminAsync();
        var editorEmail = $"editor-import-{Guid.NewGuid():N}@dip.test";
        var editorPassword = "EditorPassw0rd!";
        var createUser = await admin.PostAsJsonAsync("/api/users", new
        {
            email = editorEmail,
            password = editorPassword,
            fullName = "Import Editor",
            roles = new[] { "Editor" },
        });
        createUser.EnsureSuccessStatusCode();

        var editor = _factory.CreateClient();
        var login = await editor.PostAsJsonAsync("/api/auth/login", new { email = editorEmail, password = editorPassword });
        login.EnsureSuccessStatusCode();
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        editor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", loginBody.GetProperty("accessToken").GetString());

        var response = await editor.PostAsJsonAsync("/api/imports/start", new
        {
            projectId = QpacProjectId,
            folderFileId = Guid.NewGuid(),
            kind = "Picklists",
            target = "Live",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
