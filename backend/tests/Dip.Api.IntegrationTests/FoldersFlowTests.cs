using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Dip.Api.IntegrationTests;

// End-to-end folder CRUD + upload + permission gating against real Postgres.
// Reads the seeded QPAC project id via /api/me + a lightweight lookup query.
[Collection(IntegrationTestCollection.Name)]
public class FoldersFlowTests
{
    private static readonly Guid QpacProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly DipApiFactory _factory;

    public FoldersFlowTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_CanCreateFolderThenRenameThenSetTargetThenSeeInTree()
    {
        if (!_factory.IsPostgresAvailable) return;

        var client = await AuthedClientAsAdminAsync();

        var uniqueName = $"IntegrationRoot-{Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync("/api/folders", new
        {
            projectId = QpacProjectId,
            parentId = (Guid?)null,
            name = uniqueName,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var folderId = Guid.Parse((await create.Content.ReadAsStringAsync()).Trim('"'));

        // Set Draft target — requires folders.assignTarget which SuperAdmin has.
        var setTarget = await client.PutAsJsonAsync($"/api/folders/{folderId}/target", new { target = "Draft" });
        setTarget.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Rename it.
        var newName = uniqueName + "-Renamed";
        var rename = await client.PutAsJsonAsync($"/api/folders/{folderId}/name", new { newName });
        rename.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Fetch details and verify.
        var detail = await client.GetAsync($"/api/folders/{folderId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await detail.Content.ReadFromJsonAsync<JsonElement>();
        var folderNode = body.GetProperty("folder");
        folderNode.GetProperty("name").GetString().Should().Be(newName);
        folderNode.GetProperty("target").GetString().Should().Be("Draft");

        // Tree contains the folder.
        var tree = await client.GetAsync($"/api/projects/{QpacProjectId}/folders/tree");
        tree.StatusCode.Should().Be(HttpStatusCode.OK);
        var treeText = await tree.Content.ReadAsStringAsync();
        treeText.Should().Contain(newName);
    }

    [Fact]
    public async Task Admin_CanUploadFileAndDetectKind()
    {
        if (!_factory.IsPostgresAvailable) return;

        var client = await AuthedClientAsAdminAsync();

        // Create parent folder.
        var folderResp = await client.PostAsJsonAsync("/api/folders", new
        {
            projectId = QpacProjectId,
            parentId = (Guid?)null,
            name = $"UploadTest-{Guid.NewGuid():N}",
        });
        var folderId = Guid.Parse((await folderResp.Content.ReadAsStringAsync()).Trim('"'));

        // Multipart upload of a small "TDP" file so the auto-detect classifies it as Tidp.
        var content = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes("PKfake-xlsx-bytes");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "QF01012-NES-C04518-TDP-STL-00-000000-000001.xlsx");

        var upload = await client.PostAsync($"/api/folders/{folderId}/files", content);
        upload.StatusCode.Should().Be(HttpStatusCode.Created);

        // GET the folder — file should show up with Kind=Tidp.
        var detail = await client.GetAsync($"/api/folders/{folderId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await detail.Content.ReadFromJsonAsync<JsonElement>();
        var files = body.GetProperty("files");
        files.GetArrayLength().Should().Be(1);
        files[0].GetProperty("kind").GetString().Should().Be("Tidp");
        files[0].GetProperty("source").GetString().Should().Be("Upload");
    }

    [Fact]
    public async Task Editor_Forbidden_FromSetTarget()
    {
        if (!_factory.IsPostgresAvailable) return;

        // Provision an Editor user for this test.
        var admin = await AuthedClientAsAdminAsync();
        var editorEmail = $"editor-folder-{Guid.NewGuid():N}@dip.test";
        var editorPassword = "EditorPassw0rd!";
        var createUser = await admin.PostAsJsonAsync("/api/users", new
        {
            email = editorEmail,
            password = editorPassword,
            fullName = "Folder Editor",
            roles = new[] { "Editor" },
        });
        createUser.EnsureSuccessStatusCode();

        // Admin creates a folder to target.
        var folderResp = await admin.PostAsJsonAsync("/api/folders", new
        {
            projectId = QpacProjectId,
            parentId = (Guid?)null,
            name = $"ForbiddenTest-{Guid.NewGuid():N}",
        });
        var folderId = Guid.Parse((await folderResp.Content.ReadAsStringAsync()).Trim('"'));

        // Editor logs in and attempts SetFolderTarget — expect 403.
        var editor = _factory.CreateClient();
        var loginResp = await editor.PostAsJsonAsync("/api/auth/login", new { email = editorEmail, password = editorPassword });
        loginResp.EnsureSuccessStatusCode();
        var loginBody = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        editor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", loginBody.GetProperty("accessToken").GetString());

        var setTarget = await editor.PutAsJsonAsync($"/api/folders/{folderId}/target", new { target = "Draft" });
        setTarget.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> AuthedClientAsAdminAsync()
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
}
