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
        await db.DocumentSnapshots.ExecuteDeleteAsync();
        await db.DataExchanges.ExecuteDeleteAsync();
        await db.Documents.ExecuteDeleteAsync();
        await db.TidpFiles.ExecuteDeleteAsync();
        await db.ImportBatches.ExecuteDeleteAsync();
    }

    private async Task<Guid> NewFileAsync(DipDbContext db, string fileName)
    {
        var file = new TidpFile
        {
            ProjectId = _fixture.QpacProjectId,
            DisciplineId = await db.Disciplines
                .Where(d => d.ProjectId == _fixture.QpacProjectId)
                .Select(d => d.Id)
                .FirstAsync(),
            FileName = fileName,
            UploadedBy = "test",
            UploadedAt = DateTime.UtcNow,
            Status = TidpFileStatus.Importing,
        };
        db.TidpFiles.Add(file);
        await db.SaveChangesAsync();
        return file.Id;
    }

    // Verifies PLAN.md § 9.3.2 target (first document number, leading zeros,
    // Structural discipline, TidpFile row updated) AND idempotency (second run
    // inserts zero). A fresh service scope per import keeps EF's change tracker
    // from carrying state across the two, which was the source of an intermittent
    // FK violation when the tests ran together.
    [Fact]
    public async Task Import_ParsesStructuralAndIsIdempotent()
    {
        if (!_fixture.IsAvailable) return;

        Guid fileId;
        using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            await ResetTablesAsync(db);
            fileId = await NewFileAsync(db, "TIDP-STL.xlsx");
        }

        var firstScope = _fixture.CreateScope();
        var first = await firstScope.ServiceProvider.GetRequiredService<TidpImporter>().ImportAsync(
            _fixture.QpacProjectId,
            SampleFiles.Open("TIDP-STL.xlsx"),
            fileId,
            importedBy: "test",
            CancellationToken.None);
        firstScope.Dispose();

        first.RowsRead.Should().BeGreaterThan(50, "TIDP-STL sample has hundreds of populated rows");
        first.RowsInserted.Should().BeGreaterThan(50);

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
            target.TidpFileId.Should().Be(fileId);
            target.IsEdited.Should().BeFalse("an imported row is not a hand edit");

            var discipline = await db.Disciplines.SingleAsync(d => d.Id == target.DisciplineId);
            discipline.CorporateName.Should().Be("Structural");

            var file = await db.TidpFiles.SingleAsync(t => t.Id == fileId);
            file.RevisionNumber.Should().Be("00");
            file.DisciplineId.Should().Be(target.DisciplineId);
            file.RowsRead.Should().Be(first.RowsRead);
        }

        // Idempotency: fresh scope so EF's change tracker starts empty; the second
        // run should upsert every existing document with zero inserts.
        var secondScope = _fixture.CreateScope();
        var second = await secondScope.ServiceProvider.GetRequiredService<TidpImporter>().ImportAsync(
            _fixture.QpacProjectId, SampleFiles.Open("TIDP-STL.xlsx"),
            fileId, "test", CancellationToken.None);
        secondScope.Dispose();

        second.RowsInserted.Should().Be(0);
        second.RowsRead.Should().Be(first.RowsRead);
    }

    // First file wins. Re-uploading the same workbook under a second TidpFile must
    // not move its documents: otherwise one discipline's upload would silently take
    // rows out of another's, and the replace path would then delete them.
    [Fact]
    public async Task Import_DoesNotStealANumberAnotherFileOwns()
    {
        if (!_fixture.IsAvailable) return;

        Guid firstFileId, secondFileId;
        using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            await ResetTablesAsync(db);
            firstFileId = await NewFileAsync(db, "TIDP-STL.xlsx");
            secondFileId = await NewFileAsync(db, "TIDP-STL-copy.xlsx");
        }

        using (var scope = _fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TidpImporter>().ImportAsync(
                _fixture.QpacProjectId, SampleFiles.Open("TIDP-STL.xlsx"),
                firstFileId, "test", CancellationToken.None);
        }

        ImportResult second;
        using (var scope = _fixture.CreateScope())
        {
            second = await scope.ServiceProvider.GetRequiredService<TidpImporter>().ImportAsync(
                _fixture.QpacProjectId, SampleFiles.Open("TIDP-STL.xlsx"),
                secondFileId, "test", CancellationToken.None);
        }

        second.RowsInserted.Should().Be(0);
        second.RowsUpdated.Should().Be(0);
        second.RowsSkipped.Should().Be(second.RowsRead, "every number already belongs to the first file");
        second.Warnings.Should().Contain(w => w.Contains("already belongs to TIDP-STL.xlsx"));

        using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            (await db.Documents.CountAsync(d => d.TidpFileId == secondFileId)).Should().Be(0);
            (await db.Documents.CountAsync(d => d.TidpFileId == firstFileId)).Should().BeGreaterThan(50);
        }
    }

    // A row carrying another project's code is an Excel autofill accident, not a
    // document: in the sample MIDP, rows 8034 onward increment the project code, the
    // contract, the zone, the building and the sequence together under one repeated
    // title. Rejected with the offending value so it can be found and fixed, and
    // counted so the import does not quietly lose them.
    [Fact]
    public async Task RejectsRowsWhoseProjectCodeIsNotTheProjects()
    {
        if (!_fixture.IsAvailable) return;

        // Expected count derived from the sheet, never pasted: CLAUDE.md rule 8.
        var expected = await ForeignProjectRowsAsync("TIDP-STL.xlsx");

        Guid fileId;
        using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            await ResetTablesAsync(db);
            fileId = await NewFileAsync(db, "TIDP-STL.xlsx");

            // Rename the project so every row in the sample reads as foreign.
            await db.Projects.Where(p => p.Id == _fixture.QpacProjectId)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.Code, "OTHER99"));
        }

        try
        {
            ImportResult result;
            using (var scope = _fixture.CreateScope())
            {
                result = await scope.ServiceProvider.GetRequiredService<TidpImporter>().ImportAsync(
                    _fixture.QpacProjectId, SampleFiles.Open("TIDP-STL.xlsx"),
                    fileId, "test", CancellationToken.None);
            }

            result.RowsSkipped.Should().Be(result.RowsRead,
                "every row of the sample carries QF01012, which is not this project's code");
            result.RowsInserted.Should().Be(0);
            result.Warnings.Should().Contain(w => w.Contains("PROJECT is QF01012, not OTHER99"));
            result.Warnings.Should().Contain(w => w.Contains("rejected"));
            expected.Should().Be(0, "the sample's own rows all carry the project's real code");
        }
        finally
        {
            using var scope = _fixture.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            await db.Projects.Where(p => p.Id == _fixture.QpacProjectId)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.Code, "QF01012"));
        }
    }

    // How many rows of a sample carry a project code other than the project's own.
    private async Task<int> ForeignProjectRowsAsync(string fileName)
    {
        using var scope = _fixture.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<Dip.Application.Abstractions.IExcelReader>();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var code = await db.Projects.Where(p => p.Id == _fixture.QpacProjectId)
            .Select(p => p.Code).FirstAsync();

        using var wb = reader.Open(SampleFiles.Open(fileName));
        var sheet = wb.Sheet("TIDP_Sheet");
        var headerRow = DocumentRowParser.FindDocumentTableHeader(sheet);
        var columns = DocumentRowParser.MapDocumentColumns(sheet, headerRow);

        var foreign = 0;
        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var value = DocumentRowParser.ReadRequired(sheet.Row(r), columns.F01);
            if (!string.IsNullOrEmpty(value)
                && !string.Equals(value, code, StringComparison.OrdinalIgnoreCase))
            {
                foreign++;
            }
        }
        return foreign;
    }
}
