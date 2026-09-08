using Dip.Api.Common;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Api.IntegrationTests;

// refactor-plan § 3 R8: a Live-target file contributes its Live rows, a Draft-target
// file its draft rows, and a number present in both is settled by the newer file with
// Live breaking a tie.
[Collection(IntegrationTestCollection.Name)]
public class EffectiveDocumentLoaderTests
{
    private readonly DipApiFactory _factory;

    public EffectiveDocumentLoaderTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task LiveFolderContributesLiveRows_DraftFolderContributesDraftRows()
    {
        if (!_factory.IsPostgresAvailable) return;

        var world = await SeedAsync();
        var effective = await LoadAsync(world.ProjectId);

        var liveOnly = effective.Single(d => d.Document.DocumentNumber == world.LiveOnlyNumber);
        liveOnly.Layer.Should().Be(DataTarget.Live);

        var draftOnly = effective.Single(d => d.Document.DocumentNumber == world.DraftOnlyNumber);
        draftOnly.Layer.Should().Be(DataTarget.Draft);
        draftOnly.RowId.Should().Be(world.DraftOnlyRowId);
    }

    [Fact]
    public async Task DuplicateRowsAreExcluded()
    {
        if (!_factory.IsPostgresAvailable) return;

        var world = await SeedAsync();
        var effective = await LoadAsync(world.ProjectId);

        effective.Should().NotContain(d => d.RowId == world.DuplicateDraftRowId);
    }

    [Fact]
    public async Task SameNumberInBothLayers_NewerFileWins()
    {
        if (!_factory.IsPostgresAvailable) return;

        // The draft's file was modified after the Live file, so the draft row wins.
        var world = await SeedAsync(draftFileModified: new DateTime(2026, 3, 2), liveFileModified: new DateTime(2026, 3, 1));
        var effective = await LoadAsync(world.ProjectId);

        var shared = effective.Single(d => d.Document.DocumentNumber == world.SharedNumber);
        shared.Layer.Should().Be(DataTarget.Draft);
        shared.Document.Title.Should().Be("draft copy");
    }

    [Fact]
    public async Task SameNumberSameTimestamp_LiveWins()
    {
        if (!_factory.IsPostgresAvailable) return;

        var at = new DateTime(2026, 3, 1);
        var world = await SeedAsync(draftFileModified: at, liveFileModified: at);
        var effective = await LoadAsync(world.ProjectId);

        var shared = effective.Single(d => d.Document.DocumentNumber == world.SharedNumber);
        shared.Layer.Should().Be(DataTarget.Live);
        shared.Document.Title.Should().Be("live copy");
    }

    // ------------------------------------------------------------------ helpers

    private async Task<IReadOnlyList<EffectiveDocument>> LoadAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        return await EffectiveDocumentLoader.LoadAsync(db, projectId, CancellationToken.None);
    }

    private sealed record World(
        Guid ProjectId,
        string LiveOnlyNumber,
        string DraftOnlyNumber,
        Guid DraftOnlyRowId,
        Guid DuplicateDraftRowId,
        string SharedNumber);

    private async Task<World> SeedAsync(
        DateTime? draftFileModified = null, DateTime? liveFileModified = null)
    {
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Effective set test");
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var liveFolder = NewFolder(db, projectId, $"Live-{tag}", DataTarget.Live);
        var draftFolder = NewFolder(db, projectId, $"Draft-{tag}", DataTarget.Draft);
        var liveFile = NewFile(db, liveFolder, "TIDP-LIVE.xlsx", liveFileModified ?? new DateTime(2026, 1, 1));
        var draftFile = NewFile(db, draftFolder, "TIDP-DRAFT.xlsx", draftFileModified ?? new DateTime(2026, 1, 2));

        var tidp = new Tidp
        {
            ProjectId = projectId,
            DisciplineId = db.Disciplines.First(d => d.ProjectId == projectId).Id,
            FolderFileId = liveFile.Id,
            DocumentReference = "REF",
        };
        db.Tidps.Add(tidp);

        var tidpDraft = new TidpDraft
        {
            ProjectId = projectId,
            DisciplineId = tidp.DisciplineId,
            FolderFileId = draftFile.Id,
            ImportBatchId = Guid.NewGuid(),
            DocumentReference = "REF",
        };
        db.TidpDrafts.Add(tidpDraft);

        var liveOnly = $"QF01012-NES-C04518-SDW-{tag}-00-Z00000-0ZZ0001";
        var draftOnly = $"QF01012-NES-C04518-SDW-{tag}-00-Z00000-0ZZ0002";
        var shared = $"QF01012-NES-C04518-SDW-{tag}-00-Z00000-0ZZ0003";

        db.Documents.Add(NewDocument(projectId, tidp, liveFile.Id, liveOnly, "live only"));
        db.Documents.Add(NewDocument(projectId, tidp, liveFile.Id, shared, "live copy"));

        var draftRow = NewDraft(projectId, tidpDraft, draftFile.Id, draftOnly, "draft only");
        var duplicate = NewDraft(projectId, tidpDraft, draftFile.Id, draftOnly, "duplicate");
        duplicate.IsDuplicate = true;
        var sharedDraft = NewDraft(projectId, tidpDraft, draftFile.Id, shared, "draft copy");
        db.DocumentDrafts.AddRange(draftRow, duplicate, sharedDraft);

        await db.SaveChangesAsync();
        return new World(projectId, liveOnly, draftOnly, draftRow.Id, duplicate.Id, shared);
    }

    private static Folder NewFolder(DipDbContext db, Guid projectId, string name, DataTarget target)
    {
        var folder = new Folder { ProjectId = projectId, Name = name, Path = name, Target = target };
        db.Folders.Add(folder);
        return folder;
    }

    private static FolderFile NewFile(DipDbContext db, Folder folder, string name, DateTime modified)
    {
        var file = new FolderFile
        {
            FolderId = folder.Id,
            Name = name,
            Kind = FileKind.Tidp,
            ContentSource = FileSource.Upload,
            ContentModifiedAt = modified,
            ContentMd5 = "0",
        };
        db.FolderFiles.Add(file);
        return file;
    }

    private static Document NewDocument(Guid projectId, Tidp tidp, Guid fileId, string number, string title) => new()
    {
        ProjectId = projectId,
        TidpId = tidp.Id,
        DisciplineId = tidp.DisciplineId,
        FolderFileId = fileId,
        DocumentNumber = number,
        Title = title,
        CorporateDiscipline = "Structural",
    };

    private static DocumentDraft NewDraft(
        Guid projectId, TidpDraft tidp, Guid fileId, string number, string title) => new()
    {
        ProjectId = projectId,
        TidpDraftId = tidp.Id,
        DisciplineId = tidp.DisciplineId,
        FolderFileId = fileId,
        ImportBatchId = tidp.ImportBatchId,
        DocumentNumber = number,
        Title = title,
        CorporateDiscipline = "Structural",
    };
}
