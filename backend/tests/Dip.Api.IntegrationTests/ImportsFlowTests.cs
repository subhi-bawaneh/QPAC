using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Uploading a workbook is the whole import protocol now (decision D5): the worker
// picks it up, routes its sheets (refactor-plan § 3 R5) and writes to the layer the
// folder's target selects (R4).
[Collection(IntegrationTestCollection.Name)]
public class ImportsFlowTests
{
    private readonly DipApiFactory _factory;

    public ImportsFlowTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Upload_ToADraftFolder_WritesDraftRows_AndRecordsTheBatch()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Import draft test");
        var folderId = await TestHelpers.CreateFolderAsync(
            admin, $"ImportDraft-{Guid.NewGuid():N}", projectId: projectId);
        await TestHelpers.SetTargetAsync(admin, folderId, "Draft");

        var file = await TestHelpers.ImportedAsync(admin, folderId, "TIDP-STL.xlsx");
        var fileId = file.GetProperty("id").GetGuid();

        var expected = await SheetRowCountAsync();
        (await DraftCountAsync(fileId)).Should().Be(expected);
        (await LiveCountAsync(fileId)).Should().Be(0, "a Draft-target folder must not touch Live");

        // The batch is visible through the endpoints that survived the refactor.
        var batches = await TestHelpers.GetJsonAsync(
            admin, $"/api/projects/{projectId}/imports?kind=Tidp&take=20");
        var batch = batches.EnumerateArray()
            .First(b => b.GetProperty("folderFileId").GetGuid() == fileId);
        batch.GetProperty("target").GetString().Should().Be("Draft");
        batch.GetProperty("completed").GetBoolean().Should().BeTrue();

        var status = await TestHelpers.GetJsonAsync(
            admin, $"/api/imports/{batch.GetProperty("id").GetGuid()}/status");
        status.GetProperty("rowsRead").GetInt32().Should().Be(expected);
    }

    [Fact]
    public async Task Upload_ToALiveFolder_WritesLiveRows()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Import live test");
        var folderId = await TestHelpers.CreateFolderAsync(
            admin, $"ImportLive-{Guid.NewGuid():N}", projectId: projectId);

        var file = await TestHelpers.ImportedAsync(admin, folderId, "TIDP-STL.xlsx");
        var fileId = file.GetProperty("id").GetGuid();

        var expected = await SheetRowCountAsync();
        (await LiveCountAsync(fileId)).Should().Be(expected);
        (await DraftCountAsync(fileId)).Should().Be(0);
    }

    [Fact]
    public async Task Viewer_Forbidden_FromUploading()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var folderId = await TestHelpers.CreateFolderAsync(admin, $"ImportForbidden-{Guid.NewGuid():N}");
        var viewer = await TestHelpers.AuthedAsync(_factory, admin, "Viewer", "import-viewer");

        var response = await TestHelpers.UploadAsync(viewer, folderId, "PickLists.xlsx");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // The expected row count is derived from the workbook itself, never hard-coded
    // (CLAUDE.md rule 8).
    private static Task<int> SheetRowCountAsync() =>
        Task.FromResult(TidpSampleRowCount.Value);

    private async Task<int> DraftCountAsync(Guid fileId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.DipDbContext>();
        return await db.DocumentDrafts.CountAsync(d => d.FolderFileId == fileId);
    }

    private async Task<int> LiveCountAsync(Guid fileId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.DipDbContext>();
        return await db.Documents.CountAsync(d => d.FolderFileId == fileId);
    }
}
