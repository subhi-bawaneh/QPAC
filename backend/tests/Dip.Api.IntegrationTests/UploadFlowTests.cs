using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Uploading, replacing and deleting a source file is the super admin's alone, and a
// replace or a delete writes every outgoing row to the audit log before destroying it.
[Collection(IntegrationTestCollection.Name)]
public class UploadFlowTests
{
    private const string Xlsx =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly DipApiFactory _factory;

    public UploadFlowTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Upload_CreatesTheFileRow_AndImportsItsDocuments()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var accepted = await UploadTidpAsync(admin, "TIDP-STL.xlsx");

        var fileId = accepted.GetProperty("tidpFileId").GetGuid();
        await WaitForBatchAsync(accepted.GetProperty("batchId").GetGuid());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var file = await db.TidpFiles.AsNoTracking().SingleAsync(t => t.Id == fileId);
        file.Status.Should().Be(TidpFileStatus.Imported);
        file.FileName.Should().Be("TIDP-STL.xlsx");
        file.RowsRead.Should().BeGreaterThan(50);

        (await db.Documents.CountAsync(d => d.TidpFileId == fileId)).Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task Upload_RealTidpWorkbook_Returns202AndImportsSuccessfully()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var realFile = FindRealTidpFile();

        File.Exists(realFile).Should().BeTrue($"Real TIDP file must exist at {realFile}");

        var response = await PostRealFileAsync(
            admin, $"/api/projects/{TestHelpers.QpacProjectId}/tidp-files", realFile);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "upload of real TIDP failed: {0}", await response.Content.ReadAsStringAsync());

        var accepted = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fileId = accepted.GetProperty("tidpFileId").GetGuid();
        await WaitForBatchAsync(accepted.GetProperty("batchId").GetGuid());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var file = await db.TidpFiles.AsNoTracking().SingleAsync(t => t.Id == fileId);
        file.Status.Should().Be(TidpFileStatus.Imported);
        file.RowsRead.Should().BeGreaterThan(0);
    }

    // The whole point of the edited-row count is that it is a number, not a warning
    // string: the operator is told how much hand-entered work a replace will destroy.
    [Fact]
    public async Task ReplacePreview_CountsTheEditedRowsThatWillBeDestroyed()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var accepted = await UploadTidpAsync(admin, "TIDP-STL-AFCO.xlsx");
        var fileId = accepted.GetProperty("tidpFileId").GetGuid();
        await WaitForBatchAsync(accepted.GetProperty("batchId").GetGuid());

        // Mark two rows as hand-edited, the way the document editor does.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            var ids = await db.Documents.Where(d => d.TidpFileId == fileId)
                .OrderBy(d => d.DocumentNumber).Select(d => d.Id).Take(2).ToListAsync();
            await db.Documents.Where(d => ids.Contains(d.Id))
                .ExecuteUpdateAsync(u => u
                    .SetProperty(d => d.IsEdited, true)
                    .SetProperty(d => d.EditedBy, "engineer@dip.local")
                    .SetProperty(d => d.EditedAt, DateTime.UtcNow));
        }

        var preview = await TestHelpers.GetJsonAsync(admin, $"/api/tidp-files/{fileId}/replace-preview");

        preview.GetProperty("editedRows").GetInt32().Should().Be(2);
        preview.GetProperty("rows").GetInt32().Should().BeGreaterThan(2);
        preview.GetProperty("fileName").GetString().Should().Be("TIDP-STL-AFCO.xlsx");
    }

    [Fact]
    public async Task Replace_DumpsEveryOutgoingRowToTheAuditLogBeforeDeletingIt()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var accepted = await UploadTidpAsync(admin, "TIDP-STL-AFCO.xlsx");
        var fileId = accepted.GetProperty("tidpFileId").GetGuid();
        await WaitForBatchAsync(accepted.GetProperty("batchId").GetGuid());

        int before;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            before = await db.Documents.CountAsync(d => d.TidpFileId == fileId);
            await db.AuditLogs.Where(a => a.Action == "Replaced").ExecuteDeleteAsync();
        }

        var replaced = await ReplaceTidpAsync(admin, fileId, "TIDP-STL-AFCO.xlsx");
        await WaitForBatchAsync(replaced.GetProperty("batchId").GetGuid());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

            var dumped = await db.AuditLogs.AsNoTracking()
                .Where(a => a.Action == "Replaced")
                .ToListAsync();

            dumped.Should().HaveCount(before,
                "every row a replace destroys is recorded before it goes");
            dumped.Should().OnlyContain(a => a.OldValue != null && a.OldValue.Contains("documentNumber"));
            dumped.Should().OnlyContain(a => a.EntityName == "Document");

            // And the file still holds its rows: a replace reloads, it does not empty.
            (await db.Documents.CountAsync(d => d.TidpFileId == fileId)).Should().Be(before);
        }
    }

    [Fact]
    public async Task Delete_DumpsTheRowsAndRemovesThemFromTheRegister()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var accepted = await UploadTidpAsync(admin, "TIDP-STL-AFCO.xlsx");
        var fileId = accepted.GetProperty("tidpFileId").GetGuid();
        await WaitForBatchAsync(accepted.GetProperty("batchId").GetGuid());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            await db.AuditLogs.Where(a => a.Action == "Deleted").ExecuteDeleteAsync();
        }

        var response = await admin.DeleteAsync($"/api/tidp-files/{fileId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var removed = body.GetProperty("documentsRemoved").GetInt32();
        removed.Should().BeGreaterThan(50);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            (await db.TidpFiles.CountAsync(t => t.Id == fileId)).Should().Be(0);
            (await db.Documents.CountAsync(d => d.TidpFileId == fileId)).Should().Be(0);
            (await db.AuditLogs.CountAsync(a => a.Action == "Deleted")).Should().Be(removed);
        }
    }

    // files.manage is granted to no named role, so an Admin is refused. That is the
    // whole super-admin check: a permission nobody else holds, not a role-name test.
    [Fact]
    public async Task Admin_IsForbiddenFromUploadingReplacingOrDeleting()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var superAdmin = await TestHelpers.AuthedAdminAsync(_factory);
        var admin = await TestHelpers.AuthedAsync(_factory, superAdmin, "Admin", "upload-admin");

        var upload = await PostFileAsync(
            admin, $"/api/projects/{TestHelpers.QpacProjectId}/tidp-files", "TIDP-STL.xlsx");
        upload.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var someFile = Guid.NewGuid();
        (await admin.GetAsync($"/api/tidp-files/{someFile}/replace-preview"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.DeleteAsync($"/api/tidp-files/{someFile}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var baseline = await PostFileAsync(
            admin, $"/api/projects/{TestHelpers.QpacProjectId}/baseline/upload", "Baseline.xlsx");
        baseline.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ helpers

    // Ownership is first-file-wins, so uploading the same workbook again as a SECOND
    // TidpFile would leave it with nothing: every number already belongs to the first.
    // That is the rule working, not a test artefact — so a test that wants a populated
    // file of its own removes the previous one first, which is what an operator does.
    private async Task<JsonElement> UploadTidpAsync(HttpClient admin, string fileName)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            var stale = await db.TidpFiles
                .Where(t => t.ProjectId == TestHelpers.QpacProjectId && t.FileName == fileName)
                .ToListAsync();
            if (stale.Count > 0)
            {
                db.TidpFiles.RemoveRange(stale);
                await db.SaveChangesAsync();
            }
        }

        var response = await PostFileAsync(
            admin, $"/api/projects/{TestHelpers.QpacProjectId}/tidp-files", fileName);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "upload failed: {0}", await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> ReplaceTidpAsync(HttpClient admin, Guid fileId, string fileName)
    {
        using var multipart = new MultipartFormDataContent();
        var content = new ByteArrayContent(await File.ReadAllBytesAsync(TestHelpers.SamplePath(fileName)));
        content.Headers.ContentType = new MediaTypeHeaderValue(Xlsx);
        multipart.Add(content, "file", fileName);

        var response = await admin.PutAsync($"/api/tidp-files/{fileId}", multipart);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "replace failed: {0}", await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HttpResponseMessage> PostFileAsync(
        HttpClient client, string url, string fileName)
    {
        using var multipart = new MultipartFormDataContent();
        var content = new ByteArrayContent(await File.ReadAllBytesAsync(TestHelpers.SamplePath(fileName)));
        content.Headers.ContentType = new MediaTypeHeaderValue(Xlsx);
        multipart.Add(content, "file", fileName);
        return await client.PostAsync(url, multipart);
    }

    private static async Task<HttpResponseMessage> PostRealFileAsync(
        HttpClient client, string url, string filePath)
    {
        using var multipart = new MultipartFormDataContent();
        var content = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
        content.Headers.ContentType = new MediaTypeHeaderValue(Xlsx);
        multipart.Add(content, "file", Path.GetFileName(filePath));
        return await client.PostAsync(url, multipart);
    }

    // The import runs in the worker; the flow tests poll the batch until it settles.
    private async Task WaitForBatchAsync(Guid batchId, int timeoutSeconds = 180)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            var batch = await db.ImportBatches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch is { Status: ImportBatchStatus.Completed }) return;
            if (batch is { Status: ImportBatchStatus.Failed })
            {
                throw new Xunit.Sdk.XunitException($"Import failed: {batch.Log}");
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Batch {batchId} did not finish in {timeoutSeconds}s");
    }

    private static string FindRealTidpFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "src/Dip.Api/02.TIDPs/01.NAP/AR-Architectural/QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Cannot locate real TIDP file in 02.TIDPs folder");
    }
}
