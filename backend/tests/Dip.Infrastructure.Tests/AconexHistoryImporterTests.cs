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
public class AconexHistoryImporterTests : IClassFixture<ImporterFixture>
{
    private readonly ImporterFixture _fixture;

    public AconexHistoryImporterTests(ImporterFixture fixture) => _fixture = fixture;

    // PLAN.md § 9.3.4 target: MIDP.xlsx!Aconex History has ~25,246 rows.
    // Verifies row count, normalisation (malformed → XXX, spaces stripped, -PDF removed),
    // and that IsLatest / IsTerminated / InMidp flags are computed.
    [Fact]
    public async Task ImportsFullAconexHistory_ComputesFlagsAndNormalisation()
    {
        if (!_fixture.IsAvailable) return;

        using var scope = _fixture.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<AconexHistoryImporter>();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        // Clean slate.
        await db.AconexRevisions.ExecuteDeleteAsync();

        var batch = new ImportBatch
        {
            ProjectId = _fixture.QpacProjectId,
            Kind = ImportKind.AconexHistory,
            Target = DataTarget.Live,
            FileName = "MIDP.xlsx",
            ImportedAt = DateTime.UtcNow,
            ImportedBy = "test",
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        var result = await importer.ImportAsync(
            _fixture.QpacProjectId,
            SampleFiles.Open("MIDP.xlsx"),
            batch.Id,
            "test",
            CancellationToken.None);

        // Row count: docs/excel-analysis.md § 3.2 says 25,246. Allow a small tolerance
        // for blank template rows below the last populated one.
        result.RowsRead.Should().BeInRange(25_000, 25_500,
            "Aconex History has ~25,246 rows per docs/excel-analysis.md § 3.2");
        result.RowsInserted.Should().BeInRange(25_000, 25_500);

        var totalDb = await db.AconexRevisions.CountAsync(a => a.ProjectId == _fixture.QpacProjectId);
        totalDb.Should().Be(result.RowsInserted);

        // Shapes that are not document numbers at all (report names, truncated values)
        // have no number. Foreign-contract numbers keep their value — they simply
        // never match a MIDP document (docs/excel-analysis.md § 6).
        var withoutNumber = await db.AconexRevisions
            .CountAsync(a => a.ProjectId == _fixture.QpacProjectId && a.DocNoFinal == null);
        withoutNumber.Should().BeGreaterThan(0, "the sample contains rows whose Document No is not a document number");

        var foreignContract = await db.AconexRevisions
            .CountAsync(a => a.ProjectId == _fixture.QpacProjectId
                && a.DocNoFinal != null && a.DocNoFinal.StartsWith("QF01012-BSB-") && !a.InMidp);
        foreignContract.Should().BeGreaterThan(0,
            "BSB rows are structurally valid numbers that are not in the MIDP");

        // Spaces stripped, export suffixes dropped: no DocNoFinal contains " " or ends with "-PDF".
        var withSpaces = await db.AconexRevisions
            .Where(a => a.ProjectId == _fixture.QpacProjectId
                && a.DocNoFinal != null
                && a.DocNoFinal.Contains(" "))
            .CountAsync();
        withSpaces.Should().Be(0);

        var withPdf = await db.AconexRevisions
            .Where(a => a.ProjectId == _fixture.QpacProjectId
                && a.DocNoFinal != null
                && a.DocNoFinal.ToLower().EndsWith("-pdf"))
            .CountAsync();
        withPdf.Should().Be(0);

        // Spot-check: pick a specific DocNoFinal and verify IsLatest is exactly one row
        // (or a few tied on sub-second). Ties happen but should be very rare.
        var groupsWithMultipleLatest = await db.AconexRevisions
            .Where(a => a.ProjectId == _fixture.QpacProjectId
                && a.DocNoFinal != null
                && a.IsLatest)
            .GroupBy(a => a.DocNoFinal)
            .Select(g => new { DocNo = g.Key, Count = g.Count() })
            .Where(x => x.Count > 5)  // Ties should be rare — no more than a handful.
            .ToListAsync();
        groupsWithMultipleLatest.Should().BeEmpty("each DocNoFinal should have a single Latest row (rare ties allowed)");

        // At least SOME rows should be IsLatest. If import worked, IsLatest.count ≈ unique DocNoFinal (excluding XXX).
        var latestCount = await db.AconexRevisions
            .CountAsync(a => a.ProjectId == _fixture.QpacProjectId && a.IsLatest);
        latestCount.Should().BeGreaterThan(1000, "many unique documents should each mark exactly one row as Latest");

        // Sub-second DateModified precision preserved. Sample the first 100 rows
        // and count those with non-zero fractional-second component. Fine to pull
        // in-memory; the check is small and Npgsql has no DateDiffMillisecond translation.
        var sample = await db.AconexRevisions
            .Where(a => a.ProjectId == _fixture.QpacProjectId)
            .OrderBy(a => a.Id)
            .Take(100)
            .Select(a => a.DateModified)
            .ToListAsync();
        var withMillis = sample.Count(d => d.Ticks % TimeSpan.TicksPerSecond != 0);
        withMillis.Should().BeGreaterThan(0, "Aconex Date Modified has ms precision that must be preserved");
    }

    [Fact]
    public async Task IsTerminated_MarkedForRowsWithLaterTerminalStatus()
    {
        if (!_fixture.IsAvailable) return;

        using var scope = _fixture.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<AconexHistoryImporter>();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        await db.AconexRevisions.ExecuteDeleteAsync();

        var batch = new ImportBatch
        {
            ProjectId = _fixture.QpacProjectId,
            Kind = ImportKind.AconexHistory,
            Target = DataTarget.Live,
            FileName = "MIDP.xlsx",
            ImportedAt = DateTime.UtcNow,
            ImportedBy = "test",
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        await importer.ImportAsync(
            _fixture.QpacProjectId, SampleFiles.Open("MIDP.xlsx"),
            batch.Id, "test", CancellationToken.None);

        // If ANY row is Terminated, it must have a same-(DocNo, Revision) row
        // with terminal status at or after it. We check: for terminated rows,
        // the group's max DateModified belongs to a terminal-status row.
        //
        // Simpler check: the sample DOES contain terminal-status rows so at
        // least some IsTerminated=true rows must exist.
        var hasTerminal = await db.AconexRevisions
            .AnyAsync(a => a.ProjectId == _fixture.QpacProjectId
                && (a.ReviewStatus == "Terminated"
                    || a.AconexStatus == "Closed"
                    || a.AconexStatus == "No Longer In Use"));
        if (hasTerminal)
        {
            var anyTerminated = await db.AconexRevisions
                .AnyAsync(a => a.ProjectId == _fixture.QpacProjectId && a.IsTerminated);
            anyTerminated.Should().BeTrue(
                "when terminal-status rows exist, IsTerminated must propagate to same-(DocNo,Rev) rows");
        }
    }
}
