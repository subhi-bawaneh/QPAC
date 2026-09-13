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

// The folder sync against the real folder in src/Dip.Api/02.TIDPs.
//
// Each test gets its own project. The register is keyed per project, and a sync that
// found the whole folder in one test would find every path Missing in the next.
[Collection(IntegrationTestCollection.Name)]
public class TidpFolderSyncTests
{
    private readonly DipApiFactory _factory;

    public TidpFolderSyncTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task The_first_sync_adds_every_file_and_the_second_skips_every_one()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "tidp-folder-idempotent");

        var files = TidpSampleFolder.Files();
        var folders = TidpSampleFolder.Folders();
        files.Should().HaveCountGreaterThan(30, "the checked-in sample folder holds 36 workbooks");

        // ------------------------------------------------------------- first run
        var first = await SyncAsync(admin, projectId, files, folders);

        first.GetProperty("totalFiles").GetInt32().Should().Be(files.Count);
        first.GetProperty("added").GetInt32().Should().Be(files.Count);
        first.GetProperty("updated").GetInt32().Should().Be(0);
        first.GetProperty("skipped").GetInt32().Should().Be(0);
        first.GetProperty("missing").GetInt32().Should().Be(0);
        first.GetProperty("failed").GetInt32().Should().Be(0);

        await WaitForImportsAsync(projectId);

        // Every workbook parsed. This is the assertion that would catch a file whose
        // header the importer cannot read, which the second run would then re-import
        // and turn the idempotency check below red for a reason worth knowing.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            var failed = await db.TidpFiles.AsNoTracking()
                .Where(t => t.ProjectId == projectId && t.Status == TidpFileStatus.Failed)
                .Select(t => t.RelativePath + ": " + t.Error)
                .ToListAsync();
            failed.Should().BeEmpty();
        }

        // ------------------------------------------------------------ second run
        var second = await SyncAsync(admin, projectId, files, folders);

        second.GetProperty("added").GetInt32().Should().Be(0);
        second.GetProperty("updated").GetInt32().Should().Be(0);
        second.GetProperty("skipped").GetInt32().Should().Be(files.Count);
        second.GetProperty("missing").GetInt32().Should().Be(0);
        second.GetProperty("failed").GetInt32().Should().Be(0);

        // A skipped file queues nothing: the second run must not have made a single
        // new import batch, or "skipped" would be a label on work that still happened.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            (await db.ImportBatches.CountAsync(b => b.ProjectId == projectId))
                .Should().Be(files.Count);
            (await db.TidpFiles.CountAsync(t => t.ProjectId == projectId))
                .Should().Be(files.Count);
        }
    }

    // The tie-breaker: a file whose bytes really changed is the only one re-imported,
    // and it takes its old rows to the audit log on the way.
    [Fact]
    public async Task Changing_one_file_updates_exactly_that_one()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "tidp-folder-one-change");

        var folder = TidpSampleFolder.CopyToTemp();
        try
        {
            var folders = TidpSampleFolder.Folders(folder);
            await SyncAsync(admin, projectId, TidpSampleFolder.Files(folder), folders);
            await WaitForImportsAsync(projectId);

            // Edit one workbook in place: new bytes, new timestamp. Appending to the
            // zip container keeps it a readable .xlsx, which is the point — a file the
            // importer refuses would be Failed, not Updated.
            var target = Directory
                .EnumerateFiles(folder, "*.xlsx", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal)
                .First();
            await File.AppendAllTextAsync(target, "\n<!-- touched by the test -->");
            File.SetLastWriteTimeUtc(target, DateTime.UtcNow.AddMinutes(5));

            var second = await SyncAsync(admin, projectId, TidpSampleFolder.Files(folder), folders);

            second.GetProperty("updated").GetInt32().Should().Be(1);
            second.GetProperty("added").GetInt32().Should().Be(0);
            second.GetProperty("missing").GetInt32().Should().Be(0);
            second.GetProperty("skipped").GetInt32().Should().Be(TidpSampleFolder.Files(folder).Count - 1);

            var updated = second.GetProperty("files").EnumerateArray()
                .Single(f => f.GetProperty("action").GetString() == "Updated");
            updated.GetProperty("relativePath").GetString()
                .Should().Be(Relative(folder, target));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // A timestamp that moved with no edit behind it. The hash is the last word, and it
    // says nothing changed — so nothing is re-parsed, and nothing anyone typed into
    // those rows is destroyed for the sake of a `touch`.
    [Fact]
    public async Task A_touched_but_unchanged_file_is_skipped_not_reimported()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "tidp-folder-touched");

        var files = TidpSampleFolder.Files();
        await SyncAsync(admin, projectId, files, TidpSampleFolder.Folders());
        await WaitForImportsAsync(projectId);

        var touched = files
            .Select((f, i) => i == 0 ? f with { LastModifiedUtc = f.LastModifiedUtc.AddHours(3) } : f)
            .ToList();

        var second = await SyncAsync(admin, projectId, touched, TidpSampleFolder.Folders());

        second.GetProperty("skipped").GetInt32().Should().Be(files.Count);
        second.GetProperty("updated").GetInt32().Should().Be(0);
    }

    // A path that stops appearing is marked, never deleted — and when its name and its
    // content turn up somewhere else, both halves say so.
    [Fact]
    public async Task A_file_that_moves_between_owners_is_reported_as_a_move_and_never_deleted()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "tidp-folder-move");

        var files = TidpSampleFolder.Files();
        var folders = TidpSampleFolder.Folders();
        await SyncAsync(admin, projectId, files, folders);
        await WaitForImportsAsync(projectId);

        // The same bytes, under a different owner.
        var moving = files.First(f => f.RelativePath.Contains("05.DOKA", StringComparison.Ordinal));
        var movedTo = moving.RelativePath.Replace("05.DOKA", "03.ALUTEC", StringComparison.Ordinal);
        var next = files
            .Where(f => f.RelativePath != moving.RelativePath)
            .Append(moving with { RelativePath = movedTo })
            .ToList();

        var second = await SyncAsync(admin, projectId, next, folders);

        second.GetProperty("added").GetInt32().Should().Be(1);
        second.GetProperty("missing").GetInt32().Should().Be(1);

        var entries = second.GetProperty("files").EnumerateArray().ToList();
        var gone = entries.Single(f => f.GetProperty("action").GetString() == "Missing");
        var arrived = entries.Single(f => f.GetProperty("action").GetString() == "Added");

        gone.GetProperty("movedTo").GetString().Should().Be(Strip(movedTo));
        arrived.GetProperty("movedFrom").GetString().Should().Be(Strip(moving.RelativePath));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var old = await db.TidpFiles.AsNoTracking()
            .SingleAsync(t => t.ProjectId == projectId && t.RelativePath == Strip(moving.RelativePath));
        old.FolderStatus.Should().Be(TidpFolderStatus.Missing);
        old.MissingSince.Should().NotBeNull();
    }

    // The folder's own shape, including the five discipline folders that hold nothing.
    [Fact]
    public async Task The_folder_tree_records_owners_disciplines_and_the_empty_folders()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "tidp-folder-tree");

        await SyncAsync(admin, projectId, TidpSampleFolder.Files(), TidpSampleFolder.Folders());

        var tree = await TestHelpers.GetJsonAsync(admin, $"/api/projects/{projectId}/tidp-folder");
        var owners = tree.GetProperty("owners").EnumerateArray().ToList();

        owners.Should().HaveCount(12);
        owners.Select(o => o.GetProperty("sortOrder").GetInt32())
            .Should().BeInAscendingOrder("the folder's own numbering is the order");

        var unassigned = owners.Single(o => o.GetProperty("folderName").GetString() == "06.Subcontractor - Unassigned");
        unassigned.GetProperty("ownerType").GetString().Should().Be("Unassigned");
        unassigned.GetProperty("ownerName").ValueKind.Should().Be(JsonValueKind.Null);
        unassigned.GetProperty("files").GetArrayLength().Should().Be(6);

        var provisional = owners.Single(o => o.GetProperty("folderName").GetString() == "08. Provisional Sum");
        provisional.GetProperty("ownerType").GetString().Should().Be("ProvisionalSum");
        provisional.GetProperty("ownerName").ValueKind.Should().Be(JsonValueKind.Null);

        // Files straight under an owner folder, with no discipline folder at all.
        var jinggong = owners.Single(o => o.GetProperty("ownerName").GetString() == "JINGGONG");
        jinggong.GetProperty("disciplines").GetArrayLength().Should().Be(0);
        jinggong.GetProperty("files").GetArrayLength().Should().Be(1);

        // `01.NAP/ID-Interior Design` is empty on disk and recorded all the same.
        var nap = owners.Single(o => o.GetProperty("ownerName").GetString() == "NAP");
        var interior = nap.GetProperty("disciplines").EnumerateArray()
            .Single(d => d.GetProperty("disciplineCode").GetString() == "ID");
        interior.GetProperty("disciplineName").GetString().Should().Be("Interior Design");
        interior.GetProperty("files").GetArrayLength().Should().Be(0);
    }

    // The same file name lives under three owners. Three paths, three rows — the whole
    // reason the key is the path rather than the name.
    [Fact]
    public async Task One_file_name_under_three_owners_becomes_three_rows()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "tidp-folder-duplicates");

        await SyncAsync(admin, projectId, TidpSampleFolder.Files(), TidpSampleFolder.Folders());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        const string name = "QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx";
        var rows = await db.TidpFiles.AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.FileName == name)
            .Select(t => t.RelativePath)
            .ToListAsync();

        rows.Should().BeEquivalentTo(
        [
            $"01.NAP/AR-Architectural/{name}",
            $"08. Provisional Sum/{name}",
            $"11. NAP PMO/AR-Architectural/{name}",
        ]);
    }

    // files.manage is granted to no named role, so an Admin is refused — the same check
    // that guards every other source upload.
    [Fact]
    public async Task Admin_is_forbidden_from_syncing_the_folder()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var superAdmin = await TestHelpers.AuthedAdminAsync(_factory);
        var admin = await TestHelpers.AuthedAsync(_factory, superAdmin, "Admin", "folder-admin");

        using var body = TidpSampleFolder.Multipart(
            TidpSampleFolder.Files().Take(1).ToList(), TidpSampleFolder.Folders());
        var response = await admin.PostAsync(
            $"/api/projects/{TestHelpers.QpacProjectId}/tidp-folder/sync", body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<JsonElement> SyncAsync(
        HttpClient admin, Guid projectId,
        IReadOnlyList<TidpSampleFolder.Entry> files, IReadOnlyList<string> folders)
    {
        using var body = TidpSampleFolder.Multipart(files, folders);
        var response = await admin.PostAsync($"/api/projects/{projectId}/tidp-folder/sync", body);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "sync failed: {0}", await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // The sync returns as soon as the plan is written; the workbooks are parsed by the
    // hosted worker, so a test that checks what was imported waits for the queue.
    private async Task WaitForImportsAsync(Guid projectId, int timeoutSeconds = 600)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            var pending = await db.ImportBatches.AsNoTracking()
                .CountAsync(b => b.ProjectId == projectId
                    && (b.Status == ImportBatchStatus.Queued || b.Status == ImportBatchStatus.Running));

            if (pending == 0) return;
            await Task.Delay(250);
        }

        throw new TimeoutException($"Imports for {projectId} did not finish in {timeoutSeconds}s");
    }

    private static string Relative(string folder, string path) =>
        Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string Strip(string withRoot) =>
        withRoot[(TidpSampleFolder.RootName.Length + 1)..];
}
