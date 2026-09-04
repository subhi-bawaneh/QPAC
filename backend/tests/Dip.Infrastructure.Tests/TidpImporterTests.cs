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

    // Each test wipes the tables it touches so ordering doesn't matter and one
    // xUnit-parallelised class can share the ImporterFixture without pollution.
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

    // PLAN.md § 9.3.2 target: TIDP-STL.xlsx -> Structural, first document
    // QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004, leading zeros preserved.
    [Fact]
    public async Task LiveImport_StructuralDiscipline_FirstDocNumberAndLeadingZeros()
    {
        if (!_fixture.IsAvailable) return;

        using var scope = _fixture.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<TidpImporter>();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        await ResetTablesAsync(db);

        var result = await importer.ImportAsync(
            _fixture.QpacProjectId,
            SampleFiles.Path("TIDP-STL.xlsx"),
            DataTarget.Live,
            folderFileId: null,
            importBatchId: null,
            importedBy: "test",
            CancellationToken.None);

        result.RowsRead.Should().BeGreaterThan(50, "TIDP-STL sample has hundreds of populated rows");
        result.RowsInserted.Should().BeGreaterThan(50);
        result.RowsSkipped.Should().Be(0);

        var first = await db.Documents
            .SingleOrDefaultAsync(d => d.DocumentNumber == "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004");
        first.Should().NotBeNull("PLAN.md § 9.3.2 target must exist after Live import");
        first!.CorporateDiscipline.Should().Be("Structural");
        first.F06Zone.Should().Be("00");
        first.F08CSequence.Should().Be("0004");
        first.F05Discipline.Should().Be("STL");
        first.F04DocType.Should().Be("SDW");
        first.Title.Should().StartWith("BLADE 4");

        var discipline = await db.Disciplines.SingleAsync(d => d.Id == first.DisciplineId);
        discipline.CorporateName.Should().Be("Structural");

        var tidp = await db.Tidps.SingleAsync(t => t.ProjectId == _fixture.QpacProjectId
                                                     && t.DisciplineId == first.DisciplineId);
        tidp.RevisionNumber.Should().Be("00");
    }

    [Fact]
    public async Task LiveImport_IsIdempotent_ZeroInsertsOnSecondRun()
    {
        if (!_fixture.IsAvailable) return;

        using var scope = _fixture.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<TidpImporter>();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        await ResetTablesAsync(db);

        var first = await importer.ImportAsync(
            _fixture.QpacProjectId, SampleFiles.Path("TIDP-STL.xlsx"),
            DataTarget.Live, null, null, "test", CancellationToken.None);
        first.RowsInserted.Should().BeGreaterThan(0);

        var second = await importer.ImportAsync(
            _fixture.QpacProjectId, SampleFiles.Path("TIDP-STL.xlsx"),
            DataTarget.Live, null, null, "test", CancellationToken.None);
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
            Source = FileSource.Upload,
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
            SampleFiles.Path("TIDP-STL.xlsx"),
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
