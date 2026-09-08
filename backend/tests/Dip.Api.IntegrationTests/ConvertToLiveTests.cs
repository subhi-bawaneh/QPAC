using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Api.IntegrationTests;

// refactor-plan § 3 R7: converting a company promotes every file below the folder and
// then flips the target, and it can be run again after Drive refreshes the drafts.
[Collection(IntegrationTestCollection.Name)]
public class ConvertToLiveTests
{
    private readonly DipApiFactory _factory;

    public ConvertToLiveTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Convert_PromotesEveryFile_ThenFlipsTheTarget()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Convert test");
        var folderId = await TestHelpers.CreateFolderAsync(
            admin, $"Company-{Guid.NewGuid():N}", projectId: projectId);
        await TestHelpers.SetTargetAsync(admin, folderId, "Draft");

        var file = await TestHelpers.ImportedAsync(admin, folderId, "TIDP-STL.xlsx");
        var fileId = file.GetProperty("id").GetGuid();
        var draftCount = await DraftCountAsync(fileId);
        draftCount.Should().BeGreaterThan(0);

        var result = await ConvertAsync(admin, folderId);
        result.GetProperty("filesConverted").GetInt32().Should().Be(1);
        result.GetProperty("added").GetInt32().Should().Be(draftCount);
        result.GetProperty("conflicts").GetInt32().Should().Be(0);
        result.GetProperty("perFile")[0].GetProperty("added").GetInt32().Should().Be(draftCount);

        (await LiveCountAsync(fileId)).Should().Be(draftCount);

        var detail = await TestHelpers.GetJsonAsync(admin, $"/api/folders/{folderId}");
        detail.GetProperty("folder").GetProperty("target").GetString().Should().Be("Live");

        // A second convert after editing one draft row promotes exactly that row.
        await EditFirstDraftTitleAsync(admin, fileId, "CONVERTED TITLE");
        var second = await ConvertAsync(admin, folderId);
        second.GetProperty("updated").GetInt32().Should().Be(1);
        second.GetProperty("added").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task AfterConvert_ADriveReimport_ShowsHasNewerDraft()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Newer draft test");
        var folderId = await TestHelpers.CreateFolderAsync(
            admin, $"NewerDraft-{Guid.NewGuid():N}", projectId: projectId);
        await TestHelpers.SetTargetAsync(admin, folderId, "Draft");

        var file = await TestHelpers.ImportedAsync(admin, folderId, "TIDP-STL.xlsx");
        var fileId = file.GetProperty("id").GetGuid();

        await ConvertAsync(admin, folderId);

        var afterConvert = await FileAsync(admin, folderId, fileId);
        afterConvert.GetProperty("hasNewerDraft").GetBoolean().Should().BeFalse(
            "the promote is newer than the import that produced the draft");

        // Drive refreshes the workbook: the file is re-imported into Draft even though
        // the folder is Live now (R3), so Live is behind again.
        await MarkAsDriveSourcedAsync(fileId);
        await ReimportAsync(fileId);

        var afterReimport = await FileAsync(admin, folderId, fileId);
        afterReimport.GetProperty("hasNewerDraft").GetBoolean().Should().BeTrue();

        var tree = await TestHelpers.GetJsonAsync(admin, $"/api/projects/{projectId}/folders/tree");
        tree.EnumerateArray()
            .First(f => f.GetProperty("id").GetGuid() == folderId)
            .GetProperty("hasNewerDraft").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Viewer_IsForbidden_FromConverting()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var folderId = await TestHelpers.CreateFolderAsync(admin, $"ConvertDenied-{Guid.NewGuid():N}");
        var viewer = await TestHelpers.AuthedAsync(_factory, admin, "Viewer", "convert-viewer");

        var response = await viewer.PostAsync($"/api/folders/{folderId}/convert-to-live", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ helpers

    private static async Task<JsonElement> ConvertAsync(HttpClient admin, Guid folderId)
    {
        var response = await admin.PostAsync($"/api/folders/{folderId}/convert-to-live", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "convert failed: {0}", await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> FileAsync(HttpClient admin, Guid folderId, Guid fileId)
    {
        var detail = await TestHelpers.GetJsonAsync(admin, $"/api/folders/{folderId}");
        return detail.GetProperty("files").EnumerateArray()
            .First(f => f.GetProperty("id").GetGuid() == fileId);
    }

    private async Task EditFirstDraftTitleAsync(HttpClient admin, Guid fileId, string title)
    {
        var page = await TestHelpers.GetJsonAsync(
            admin, $"/api/drafts/documents?folderFileId={fileId}&pageSize=1");
        var row = page.GetProperty("items")[0];

        var payload = new Dictionary<string, object?> { ["title"] = title };
        foreach (var name in new[]
                 {
                     "extractedFromModel", "scopeArea", "authoringSoftware", "exchangeFormat", "scale",
                     "deliveryMilestone", "packageName", "activityId", "classificationCode",
                     "f01Project", "f02Originator", "f03Contract", "f04DocType", "f05Discipline",
                     "f06Zone", "f07Building", "f08ADrawingType", "f08BLevel", "f08CSequence",
                     "corporateDiscipline",
                 })
        {
            var value = row.GetProperty(name);
            payload[name] = value.ValueKind == JsonValueKind.Null ? null : value.GetString();
        }

        var response = await admin.PutAsJsonAsync(
            $"/api/drafts/documents/{row.GetProperty("id").GetGuid()}", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "draft edit failed: {0}", await response.Content.ReadAsStringAsync());
    }

    private async Task MarkAsDriveSourcedAsync(Guid fileId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var file = await db.FolderFiles.FirstAsync(f => f.Id == fileId);
        file.ContentSource = FileSource.Drive;
        file.DriveFileId = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
    }

    private async Task ReimportAsync(Guid fileId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Workers.FileImportService>()
            .ImportAsync(fileId, CancellationToken.None);
    }

    private async Task<int> DraftCountAsync(Guid fileId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        return await db.DocumentDrafts.CountAsync(d => d.FolderFileId == fileId && !d.IsDuplicate);
    }

    private async Task<int> LiveCountAsync(Guid fileId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        return await db.Documents.CountAsync(d => d.FolderFileId == fileId);
    }
}
