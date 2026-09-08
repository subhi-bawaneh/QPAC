using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class TidpImporterTests : IClassFixture<ImporterFixture>
{
    private readonly ImporterFixture _fixture;

    public TidpImporterTests(ImporterFixture fixture) => _fixture = fixture;

    private static async Task ResetTablesAsync(DipDbContext db)
    {
        await db.DocumentDrafts.ExecuteDeleteAsync();
        await db.TidpDrafts.ExecuteDeleteAsync();
        await db.DataExchanges.ExecuteDeleteAsync();
        await db.Documents.ExecuteDeleteAsync();
        await db.Tidps.ExecuteDeleteAsync();
        await db.ImportBatches.ExecuteDeleteAsync();
        await db.FolderFiles.ExecuteDeleteAsync();
        await db.Folders.ExecuteDeleteAsync();
    }

    // Consolidated Live-target test — verifies PLAN.md § 9.3.2 target (first
    // document number, leading zeros, Structural discipline, Tidp row created)
    // AND idempotency (second run inserts zero). Using a fresh service scope
    // for each import call keeps EF's change-tracker from carrying state
    // across the two imports, which was the source of an intermittent FK
    // violation when the tests ran together.
    [Fact]
    public async Task LiveImport_ParsesStructuralAndIsIdempotent()
    {
        if (!_fixture.IsAvailable) return;

        using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            await ResetTablesAsync(db);
        }

        var firstScope = _fixture.CreateScope();
        var first = await firstScope.ServiceProvider.GetRequiredService<TidpImporter>().ImportAsync(
            _fixture.QpacProjectId,
            SampleFiles.Open("TIDP-STL.xlsx"),
            DataTarget.Live,
            folderFileId: null,
            importBatchId: null,
            importedBy: "test",
            CancellationToken.None);
        firstScope.Dispose();

        first.RowsRead.Should().BeGreaterThan(50, "TIDP-STL sample has hundreds of populated rows");
        first.RowsInserted.Should().BeGreaterThan(50);
        first.RowsSkipped.Should().Be(0);

        // PLAN.md § 9.3.2 target: first document = QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004,
        // Structural discipline, leading zeros preserved (Zone "00", Sequence "0004").
        using (var assertScope = _fixture.CreateScope())
        {
            var db = assertScope.ServiceProvider.GetRequiredService<DipDbContext>();
            var target = await db.Documents
                .SingleOrDefaultAsync(d => d.DocumentNumber == "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004");
            target.Should().NotBeNull();
            target!.CorporateDiscipline.Should().Be("Structural");
            target.F06Zone.Should().Be("00");
            target.F08CSequence.Should().Be("0004");
            target.F05Discipline.Should().Be("STL");
            target.F04DocType.Should().Be("SDW");
            target.Title.Should().StartWith("BLADE 4");

            var discipline = await db.Disciplines.SingleAsync(d => d.Id == target.DisciplineId);
            discipline.CorporateName.Should().Be("Structural");

            var tidp = await db.Tidps.SingleAsync(t => t.ProjectId == _fixture.QpacProjectId
                                                         && t.DisciplineId == target.DisciplineId);
            tidp.RevisionNumber.Should().Be("00");
        }

        // Idempotency: fresh scope for the second import so EF's change tracker
        // starts empty; second run should upsert every existing document with
        // zero inserts.
        var secondScope = _fixture.CreateScope();
        var second = await secondScope.ServiceProvider.GetRequiredService<TidpImporter>().ImportAsync(
            _fixture.QpacProjectId, SampleFiles.Open("TIDP-STL.xlsx"),
            DataTarget.Live, null, null, "test", CancellationToken.None);
        secondScope.Dispose();

        second.RowsInserted.Should().Be(0);
        second.RowsRead.Should().Be(first.RowsRead);
    }

    [Fact]
    public async Task DraftImport_OnEmptyLive_AllRowsAreNew()
    {
        if (!_fixture.IsAvailable) return;

        using var scope = _fixture.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<TidpImporter>();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        await ResetTablesAsync(db);

        var folder = new Folder
        {
            ProjectId = _fixture.QpacProjectId,
            Name = "DraftTest",
            Path = "DraftTest",
            Target = DataTarget.Draft,
        };
        db.Folders.Add(folder);
        var file = new FolderFile
        {
            FolderId = folder.Id,
            Name = "TIDP-STL.xlsx",
            Kind = FileKind.Tidp,
            ContentSource = FileSource.Upload,
            State = ImportState.NotImported,
        };
        db.FolderFiles.Add(file);
        var batch = new ImportBatch
        {
            ProjectId = _fixture.QpacProjectId,
            Kind = ImportKind.Tidp,
            Target = DataTarget.Draft,
            FolderFileId = file.Id,
            FileName = "TIDP-STL.xlsx",
            ImportedAt = DateTime.UtcNow,
            ImportedBy = "test",
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        var result = await importer.ImportAsync(
            _fixture.QpacProjectId,
            SampleFiles.Open("TIDP-STL.xlsx"),
            DataTarget.Draft,
            folderFileId: file.Id,
            importBatchId: batch.Id,
            importedBy: "test",
            CancellationToken.None);

        result.RowsRead.Should().BeGreaterThan(50);
        result.RowsInserted.Should().Be(result.RowsRead - result.RowsSkipped,
            "with empty Live, every draft row should be State=New");

        var drafts = await db.DocumentDrafts
            .Where(d => d.FolderFileId == file.Id)
            .ToListAsync();
        drafts.Should().OnlyContain(d => d.State == DraftRowState.New);
        drafts.Should().Contain(d => d.DocumentNumber == "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004");
    }
}
