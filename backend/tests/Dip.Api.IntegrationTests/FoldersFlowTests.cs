using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Folder creation, upload and the automatic import that follows it, against real
// Postgres. Rename and delete are gone (decision D6/D7), so what is left to prove
// is the upsert rule of refactor-plan § 3 R1/R2 and the permission gates.
[Collection(IntegrationTestCollection.Name)]
public class FoldersFlowTests
{
    private readonly DipApiFactory _factory;

    public FoldersFlowTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_CreatesFolder_SetsTarget_AndSeesItInTheTree()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var name = $"IntegrationRoot-{Guid.NewGuid():N}";
        var folderId = await TestHelpers.CreateFolderAsync(admin, name);

        await TestHelpers.SetTargetAsync(admin, folderId, "Draft");

        var detail = await TestHelpers.GetJsonAsync(admin, $"/api/folders/{folderId}");
        var folder = detail.GetProperty("folder");
        folder.GetProperty("name").GetString().Should().Be(name);
        folder.GetProperty("target").GetString().Should().Be("Draft");

        var tree = await admin.GetAsync($"/api/projects/{TestHelpers.QpacProjectId}/folders/tree");
        tree.StatusCode.Should().Be(HttpStatusCode.OK);
        (await tree.Content.ReadAsStringAsync()).Should().Contain(name);
    }

    [Fact]
    public async Task Upload_ImportsAutomatically_AndTheSameNameReplacesTheRow()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var folderId = await TestHelpers.CreateFolderAsync(admin, $"UploadTest-{Guid.NewGuid():N}");

        var fileId = await TestHelpers.UploadSampleAsync(admin, folderId, "PickLists.xlsx");
        var file = await TestHelpers.WaitForImportAsync(admin, folderId, fileId);

        file.GetProperty("kind").GetString().Should().Be("Picklists");
        file.GetProperty("contentSource").GetString().Should().Be("Upload");
        file.GetProperty("state").GetString().Should().Be("Imported",
            "import error: {0}", file.GetProperty("importError").GetString());
        file.GetProperty("lastImportedAt").ValueKind.Should().NotBe(JsonValueKind.Null);

        // Same name again: one row, replaced, and re-imported.
        var second = await TestHelpers.UploadAsync(admin, folderId, "PickLists.xlsx");
        second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("replaced").GetBoolean().Should().BeTrue();
        body.GetProperty("fileId").GetGuid().Should().Be(fileId, "the (folder, name) pair identifies the file");

        await TestHelpers.WaitForImportAsync(admin, folderId, fileId);
        var detail = await TestHelpers.GetJsonAsync(admin, $"/api/folders/{folderId}");
        detail.GetProperty("files").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Upload_OfAnUnrecognisedWorkbook_Is400()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var folderId = await TestHelpers.CreateFolderAsync(admin, $"BadUpload-{Guid.NewGuid():N}");

        var response = await TestHelpers.UploadAsync(
            admin, folderId, "something-else.xlsx", Encoding.UTF8.GetBytes("PKfake"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateFolder_UnderADriveFolder_IsRejected()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var driveFolderId = await SeedDriveFolderAsync();

        var response = await admin.PostAsJsonAsync("/api/folders", new
        {
            projectId = TestHelpers.QpacProjectId,
            parentId = driveFolderId,
            name = $"Manual-{Guid.NewGuid():N}",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Google Drive");
    }

    [Fact]
    public async Task Editor_Forbidden_FromSetTarget()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var folderId = await TestHelpers.CreateFolderAsync(admin, $"ForbiddenTest-{Guid.NewGuid():N}");
        var editor = await TestHelpers.AuthedAsync(_factory, admin, "Editor", "folder-editor");

        var response = await editor.PutAsJsonAsync($"/api/folders/{folderId}/target", new { target = "Draft" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // A Drive-linked folder cannot be produced through the API by design, so it is
    // written straight to the database.
    private async Task<Guid> SeedDriveFolderAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.DipDbContext>();
        var folder = new Domain.Entities.Folder
        {
            ProjectId = TestHelpers.QpacProjectId,
            Name = $"DriveMirror-{Guid.NewGuid():N}",
            DriveFolderId = Guid.NewGuid().ToString("N"),
        };
        folder.Path = folder.Name;
        db.Folders.Add(folder);
        await db.SaveChangesAsync();
        return folder.Id;
    }
}
