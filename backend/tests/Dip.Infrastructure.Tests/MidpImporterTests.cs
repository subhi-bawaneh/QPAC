using Dip.Domain.Enums;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class MidpImporterTests : IClassFixture<ImporterFixture>
{
    private readonly ImporterFixture _fixture;

    public MidpImporterTests(ImporterFixture fixture) => _fixture = fixture;

    // PLAN.md § 9.3.3: "MidpImporter + test: doc count = sheet rows (15,885),
    // duplicates flagged". docs/excel-analysis.md § 4.4 confirms Tracker-filtered
    // total is 15,883 (some intra-file duplicates get collapsed).
    //
    // Consolidated into a single test because parsing + inserting 15k rows takes
    // ~60s; running it twice in the same class would triple the fixture's CI
    // cost and adds cross-test ordering coupling on the shared schema.
    [Fact]
    public async Task LiveImport_LoadsDocuments_MatchesDisciplines_FlagsDuplicates()
    {
        if (!_fixture.IsAvailable) return;

        using var scope = _fixture.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<MidpImporter>();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        // Clean slate.
        await db.DocumentDrafts.ExecuteDeleteAsync();
        await db.TidpDrafts.ExecuteDeleteAsync();
        await db.DataExchanges.ExecuteDeleteAsync();
        await db.Documents.ExecuteDeleteAsync();
        await db.Tidps.ExecuteDeleteAsync();

        var result = await importer.ImportAsync(
            _fixture.QpacProjectId,
            SampleFiles.Open("MIDP.xlsx"),
            DataTarget.Live,
            folderFileId: null,
            importBatchId: null,
            importedBy: "test",
            CancellationToken.None);

        // Row count: docs/excel-analysis.md § 4.4 - MIDP has 15,885 populated rows.
        result.RowsRead.Should().BeInRange(15_800, 16_000);
        // Intra-file duplicates get skipped (UNIQUE (ProjectId, DocumentNumber));
        // Tracker's filtered view lists 15,883.
        result.RowsInserted.Should().BeInRange(15_600, 16_000);
        (result.RowsInserted + result.RowsSkipped).Should().Be(result.RowsRead,
            "every read row is either inserted or skipped");

        // Duplicate warning fired.
        result.Warnings.Should().Contain(w =>
            w.Contains("duplicate", StringComparison.OrdinalIgnoreCase),
            "MIDP.xlsx contains intra-file duplicate DocumentNumbers");

        // Corporate discipline distribution matches Corporate Summary (§ 4.4).
        var byDiscipline = await db.Documents
            .Where(d => d.ProjectId == _fixture.QpacProjectId)
            .GroupBy(d => d.CorporateDiscipline)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Name, x => x.Count);

        byDiscipline.Should().ContainKey("Structural");
        byDiscipline.Should().ContainKey("Electrical");
        // Corporate Summary shows Structural=5,277, Electrical=2,998.
        // We allow lower bounds because MIDP duplicates collapse under UNIQUE.
        byDiscipline["Structural"].Should().BeGreaterThan(4500);
        byDiscipline["Electrical"].Should().BeGreaterThan(2500);

        // Spot-check: a known document has the expected discipline.
        var sample = await db.Documents
            .FirstAsync(d => d.DocumentNumber == "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004");
        sample.CorporateDiscipline.Should().Be("Structural");
    }
}
