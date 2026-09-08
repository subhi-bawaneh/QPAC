using Dip.Api.Workers;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Layer routing (refactor-plan § 3 R3/R4) and sheet routing (R5).
[Collection(IntegrationTestCollection.Name)]
public class FileImportServiceTests
{
    private readonly DipApiFactory _factory;

    public FileImportServiceTests(DipApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(FileKind.Tidp, FileSource.Drive, DataTarget.Live, DataTarget.Draft)]
    [InlineData(FileKind.Midp, FileSource.Drive, DataTarget.Live, DataTarget.Draft)]
    [InlineData(FileKind.Tidp, FileSource.Upload, DataTarget.Live, DataTarget.Live)]
    [InlineData(FileKind.Tidp, FileSource.Upload, DataTarget.Draft, DataTarget.Draft)]
    [InlineData(FileKind.AconexHistory, FileSource.Drive, DataTarget.Draft, DataTarget.Live)]
    [InlineData(FileKind.Baseline, FileSource.Upload, DataTarget.Draft, DataTarget.Live)]
    [InlineData(FileKind.Picklists, FileSource.Drive, DataTarget.Draft, DataTarget.Live)]
    public void LayerFor_FollowsTheSourceAndTheFolderTarget(
        FileKind kind, FileSource source, DataTarget folderTarget, DataTarget expected) =>
        FileImportService.LayerFor(kind, source, folderTarget).Should().Be(expected);

    [Fact]
    public async Task DriveSourcedTidp_InALiveFolder_LandsInDraft()
    {
        if (!_factory.IsPostgresAvailable) return;

        var fileId = await SeedAsync("TIDP-STL.xlsx", FileSource.Drive, DataTarget.Live);
        await ImportAsync(fileId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        (await db.DocumentDrafts.CountAsync(d => d.FolderFileId == fileId))
            .Should().BeGreaterThan(0, "Drive never writes the Live layer");
        (await db.Documents.CountAsync(d => d.FolderFileId == fileId)).Should().Be(0);

        var batch = await db.ImportBatches.SingleAsync(b => b.FolderFileId == fileId);
        batch.Target.Should().Be(DataTarget.Draft);
        batch.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task UploadSourcedTidp_InALiveFolder_LandsInLive()
    {
        if (!_factory.IsPostgresAvailable) return;

        var fileId = await SeedAsync("TIDP-STL.xlsx", FileSource.Upload, DataTarget.Live);
        await ImportAsync(fileId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        (await db.Documents.CountAsync(d => d.FolderFileId == fileId)).Should().BeGreaterThan(0);
        (await db.DocumentDrafts.CountAsync(d => d.FolderFileId == fileId)).Should().Be(0);
    }

    [Fact]
    public async Task MidpWorkbook_AlsoImportsItsAconexHistory()
    {
        if (!_factory.IsPostgresAvailable) return;

        var fileId = await SeedAsync("MIDP.xlsx", FileSource.Upload, DataTarget.Live);
        await ImportAsync(fileId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var batch = await db.ImportBatches.SingleAsync(b => b.FolderFileId == fileId);
        batch.Kind.Should().Be(ImportKind.Midp);
        (await db.Documents.CountAsync(d => d.FolderFileId == fileId)).Should().BeGreaterThan(0);
        (await db.AconexRevisions.CountAsync(a => a.ImportBatchId == batch.Id))
            .Should().BeGreaterThan(0, "the Aconex History sheet rides along in the MIDP workbook");
    }

    [Fact]
    public async Task UnknownWorkbook_FailsWithAnExplanation()
    {
        if (!_factory.IsPostgresAvailable) return;

        var fileId = await SeedAsync("PickLists.xlsx", FileSource.Upload, DataTarget.Live, kind: FileKind.Unknown);
        await ImportAsync(fileId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var file = await db.FolderFiles.SingleAsync(f => f.Id == fileId);
        file.State.Should().Be(ImportState.Failed);
        file.ImportError.Should().Be(FileImportService.UnrecognisedWorkbook);
    }

    // ------------------------------------------------------------------ helpers

    private async Task ImportAsync(Guid fileId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FileImportService>()
            .ImportAsync(fileId, CancellationToken.None);
    }

    private async Task<Guid> SeedAsync(
        string sampleFile, FileSource source, DataTarget target, FileKind? kind = null)
    {
        // Its own project: these imports write Live rows that would otherwise leak
        // into the promote and report tests sharing the seeded QPAC project.
        var projectId = await TestHelpers.NewProjectAsync(_factory, "File import test");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var folder = new Folder
        {
            ProjectId = projectId,
            Name = $"ImportSvc-{Guid.NewGuid():N}",
            Target = target,
        };
        folder.Path = folder.Name;
        db.Folders.Add(folder);

        var bytes = await File.ReadAllBytesAsync(TestHelpers.SamplePath(sampleFile));
        var file = new FolderFile
        {
            FolderId = folder.Id,
            Name = sampleFile,
            Kind = kind ?? Application.Files.FileKindDetector.Detect(sampleFile),
            ContentSource = source,
            ContentModifiedAt = DateTime.UtcNow,
            ContentMd5 = FakeDriveClient.Md5(bytes),
            SizeBytes = bytes.LongLength,
            State = ImportState.NotImported,
        };
        db.FolderFiles.Add(file);
        db.FileBlobs.Add(new FileBlob { FolderFileId = file.Id, Content = bytes });
        await db.SaveChangesAsync();
        return file.Id;
    }
}
