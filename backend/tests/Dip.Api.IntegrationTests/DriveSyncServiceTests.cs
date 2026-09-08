using System.Text;
using Dip.Api.Hubs;
using Dip.Api.Workers;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Drive;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dip.Api.IntegrationTests;

// The Drive mirror rules of refactor-plan § 3 R2 and § 5.3, against real Postgres
// with an in-memory Drive.
[Collection(IntegrationTestCollection.Name)]
public class DriveSyncServiceTests
{
    private readonly DipApiFactory _factory;

    public DriveSyncServiceTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task NewFolderAndFile_AreMirrored_AndTheFileIsQueued()
    {
        if (!_factory.IsPostgresAvailable) return;

        var world = await NewWorldAsync();
        world.Drive.AddFolder(world.RootId, "sub-1", "TIDPs");
        var bytes = Encoding.UTF8.GetBytes("workbook-v1");
        world.Drive.AddFile("sub-1", "file-1", "QF01012-NES-C04518-TDP-STL.xlsx", bytes, Now);

        var result = await world.SyncAsync();

        result.Error.Should().BeNull();
        result.FilesQueued.Should().Be(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var folder = await db.Folders.SingleAsync(f => f.DriveFolderId == "sub-1");
        folder.Path.Should().Be("DriveRoot/TIDPs");

        var file = await db.FolderFiles.SingleAsync(f => f.DriveFileId == "file-1");
        file.Kind.Should().Be(FileKind.Tidp);
        file.ContentSource.Should().Be(FileSource.Drive);
        file.ContentMd5.Should().Be(FakeDriveClient.Md5(bytes));
        file.State.Should().Be(ImportState.NotImported);

        var blob = await db.FileBlobs.SingleAsync(b => b.FolderFileId == file.Id);
        blob.Content.Should().Equal(bytes);

        world.Queue.PendingCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UnchangedMd5_IsNotDownloadedTwice()
    {
        if (!_factory.IsPostgresAvailable) return;

        var world = await NewWorldAsync();
        var bytes = Encoding.UTF8.GetBytes("workbook-stable");
        world.Drive.AddFile(world.RootId, "file-2", "Tracker.xlsx", bytes, Now);

        await world.SyncAsync();
        world.Drive.Downloads.Should().Be(1);

        // Same md5, later timestamp: Drive re-touched the file but the bytes are ours already.
        world.Drive.SetFile(world.RootId, "file-2", "Tracker.xlsx", bytes, Now.AddHours(1));
        var second = await world.SyncAsync();

        world.Drive.Downloads.Should().Be(1, "identical content must not be downloaded again");
        second.FilesQueued.Should().Be(0);
    }

    [Fact]
    public async Task DriveContentOlderThanAnUpload_DoesNotReplaceIt()
    {
        if (!_factory.IsPostgresAvailable) return;

        var world = await NewWorldAsync();
        var driveBytes = Encoding.UTF8.GetBytes("drive-copy");
        world.Drive.AddFile(world.RootId, "file-3", "Baseline.xlsx", driveBytes, Now.AddDays(-2));
        await world.SyncAsync();

        // Someone uploads a newer copy of the same file through the UI.
        var uploadBytes = Encoding.UTF8.GetBytes("uploaded-copy");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            var file = await db.FolderFiles.SingleAsync(f => f.DriveFileId == "file-3");
            file.ContentSource = FileSource.Upload;
            file.ContentModifiedAt = Now;
            file.ContentMd5 = FakeDriveClient.Md5(uploadBytes);
            var blob = await db.FileBlobs.SingleAsync(b => b.FolderFileId == file.Id);
            blob.Content = uploadBytes;
            await db.SaveChangesAsync();
        }

        var downloadsBefore = world.Drive.Downloads;
        await world.SyncAsync();

        world.Drive.Downloads.Should().Be(downloadsBefore, "the Drive copy is older than the upload");

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<DipDbContext>();
        var after = await checkDb.FolderFiles.SingleAsync(f => f.DriveFileId == "file-3");
        after.ContentSource.Should().Be(FileSource.Upload);
        var afterBlob = await checkDb.FileBlobs.SingleAsync(b => b.FolderFileId == after.Id);
        afterBlob.Content.Should().Equal(uploadBytes);
    }

    [Fact]
    public async Task FileRemovedFromDrive_IsSoftDeleted()
    {
        if (!_factory.IsPostgresAvailable) return;

        var world = await NewWorldAsync();
        world.Drive.AddFile(world.RootId, "file-4", "PickLists.xlsx",
            Encoding.UTF8.GetBytes("picklists"), Now);
        await world.SyncAsync();

        world.Drive.Remove(world.RootId, "file-4");
        await world.SyncAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var file = await db.FolderFiles.SingleAsync(f => f.DriveFileId == "file-4");
        file.IsDeleted.Should().BeTrue();
    }

    // ------------------------------------------------------------------ helpers

    private static DateTime Now => new(2026, 9, 8, 12, 0, 0, DateTimeKind.Unspecified);

    private sealed record World(Guid ProjectId, string RootId, FakeDriveClient Drive, WorkQueue Queue)
    {
        public Func<Task<DriveSyncResult>> Sync { get; init; } = () => throw new InvalidOperationException();

        public Task<DriveSyncResult> SyncAsync() => Sync();
    }

    // Each test gets its own project so the "DriveRoot" folder path stays unique.
    private async Task<World> NewWorldAsync()
    {
        var projectId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            db.Projects.Add(new Project
            {
                Id = projectId,
                Code = $"SYNC{Guid.NewGuid():N}"[..12],
                Name = "Drive sync test",
            });
            await db.SaveChangesAsync();
        }

        var rootId = "root-" + Guid.NewGuid().ToString("N")[..8];
        var drive = new FakeDriveClient();
        drive.Children(rootId);
        var queue = new WorkQueue();

        return new World(projectId, rootId, drive, queue)
        {
            Sync = async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var service = new DriveSyncService(
                    scope.ServiceProvider.GetRequiredService<DipDbContext>(),
                    drive,
                    queue,
                    new NoopSyncNotifier(),
                    Options.Create(new GoogleDriveOptions { RootFolderId = rootId }),
                    NullLogger<DriveSyncService>.Instance);
                return await service.SyncProjectAsync(projectId, CancellationToken.None);
            },
        };
    }
}
