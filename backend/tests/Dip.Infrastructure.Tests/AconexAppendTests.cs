using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Infrastructure.Tests;

// An Aconex upload appends. The owner exports periodically and cannot remember what
// was already loaded, so the same export handed in twice must change nothing, and two
// overlapping exports must leave one latest revision per drawing rather than two.
[Trait("Category", "Integration")]
public class AconexAppendTests : IClassFixture<ImporterFixture>
{
    private readonly ImporterFixture _fixture;

    public AconexAppendTests(ImporterFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ReUploadingTheSameExport_InsertsNothingAndCountsEveryLineAsDuplicate()
    {
        if (!_fixture.IsAvailable) return;

        await ResetAsync();

        var first = await ImportAsync("Tracker.xlsx");
        first.RowsInserted.Should().BeGreaterThan(25_000);
        first.RowsDuplicate.Should().Be(0, "nothing was held before the first upload");

        var second = await ImportAsync("Tracker.xlsx");

        second.RowsRead.Should().Be(first.RowsRead);
        second.RowsInserted.Should().Be(0);
        second.RowsDuplicate.Should().Be(second.RowsRead,
            "every line of a re-uploaded export is already held");

        (await CountAsync()).Should().Be(first.RowsInserted,
            "an append that finds nothing new must not grow the table");
    }

    // The project-wide delete this replaced was the single most dangerous line in the
    // importer: it made the log only ever as complete as the last export.
    [Fact]
    public async Task Appending_NeverDeletes_RowsFromAnEarlierBatchSurvive()
    {
        if (!_fixture.IsAvailable) return;

        await ResetAsync();
        await ImportAsync("Tracker.xlsx");

        var batchIds = await BatchIdsAsync();
        batchIds.Should().HaveCount(1);

        await ImportAsync("MIDP.xlsx");

        var after = await BatchIdsAsync();
        after.Should().Contain(batchIds[0], "the first batch's rows are still there");
    }

    // Two exports that overlap must leave exactly one row marked latest per drawing.
    // Marking all rows that share the maximum date — which the old code did — lets the
    // tracker pick between them arbitrarily once a re-export corrects a title without
    // moving the clock.
    [Fact]
    public async Task OverlappingExports_LeaveExactlyOneLatestPerDocument()
    {
        if (!_fixture.IsAvailable) return;

        await ResetAsync();
        await ImportAsync("Tracker.xlsx");
        await ImportAsync("MIDP.xlsx");

        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var offenders = await db.AconexRevisions.AsNoTracking()
            .Where(a => a.ProjectId == _fixture.QpacProjectId && a.IsLatest && a.DocNoFinal != null)
            .GroupBy(a => a.DocNoFinal!)
            .Where(g => g.Count() > 1)
            .Select(g => new { Number = g.Key, Count = g.Count() })
            .ToListAsync();

        offenders.Should().BeEmpty(
            "every document has one latest revision, whatever the exports overlapped on");

        // And every document that has any history has one, so the flag is not simply
        // switched off everywhere.
        var documents = await db.AconexRevisions.AsNoTracking()
            .Where(a => a.ProjectId == _fixture.QpacProjectId && a.DocNoFinal != null)
            .Select(a => a.DocNoFinal!)
            .Distinct()
            .CountAsync();
        var latest = await db.AconexRevisions.AsNoTracking()
            .CountAsync(a => a.ProjectId == _fixture.QpacProjectId && a.IsLatest);

        latest.Should().Be(documents);
    }

    // A second export can carry a corrected title for an event already held. That is a
    // different line, so it is inserted — and the tie it creates on DateModified is
    // broken by batch, not left for the tracker to guess at.
    [Fact]
    public async Task ACorrectedLine_IsInsertedAndWinsTheLatestTie()
    {
        if (!_fixture.IsAvailable) return;

        await ResetAsync();

        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var importer = scope.ServiceProvider.GetRequiredService<AconexHistoryImporter>();

        var number = "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ9001";
        var when = new DateTime(2026, 3, 14, 9, 30, 15, 123, DateTimeKind.Unspecified);

        var oldBatch = await NewBatchAsync(db, "first-export.xlsx");
        db.AconexRevisions.Add(Revision(oldBatch, number, when, "ORIGINAL TITLE"));
        await db.SaveChangesAsync();

        var newBatch = await NewBatchAsync(db, "second-export.xlsx");
        db.AconexRevisions.Add(Revision(newBatch, number, when, "CORRECTED TITLE"));
        await db.SaveChangesAsync();

        await importer.RecomputeFlagsAsync(_fixture.QpacProjectId, CancellationToken.None);

        var rows = await db.AconexRevisions.AsNoTracking()
            .Where(a => a.DocNoFinal == number)
            .ToListAsync();

        rows.Should().HaveCount(2, "a corrected title is a different line, not a duplicate");
        rows.Count(r => r.IsLatest).Should().Be(1);
        rows.Single(r => r.IsLatest).Title.Should().Be("CORRECTED TITLE",
            "the newer batch wins a tie on DateModified");
    }

    [Fact]
    public void TwoLinesDifferingOnlyByTitle_HashDifferently()
    {
        var when = new DateTime(2026, 3, 14, 9, 30, 15, 123);

        var a = AconexLineHasher.Compute("pdf", "f.pdf", "DOC-1", "00", "ORIGINAL", "Issued", null,
            when, null, null, null, null, null, null);
        var b = AconexLineHasher.Compute("pdf", "f.pdf", "DOC-1", "00", "CORRECTED", "Issued", null,
            when, null, null, null, null, null, null);

        a.Should().NotBe(b);
    }


    // The S3 migration backfills LineHash in SQL, because re-hashing 25k existing rows
    // through C# would mean loading them all into a migration. That makes the SQL a
    // second implementation of AconexLineHasher, and a second implementation that
    // drifts would silently re-insert the entire history on the next upload. This
    // pins the two together against the real export.
    [Fact]
    public async Task TheMigrationsSqlBackfill_ProducesTheSameHashAsTheHasher()
    {
        if (!_fixture.IsAvailable) return;

        await ResetAsync();
        await ImportAsync("Tracker.xlsx");

        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var hash = Dip.Infrastructure.Persistence.AconexLineHashSql.HashExpression("a");

        // EF1002 guards against interpolating user input into SQL. Nothing here comes
        // from a user: the expression is built from a constant column list.
#pragma warning disable EF1002
        var mismatches = await db.Database
            .SqlQueryRaw<int>(
                $@"SELECT count(*)::int AS ""Value"" FROM ""AconexRevisions"" a
                   WHERE a.""LineHash"" <> {hash}")
            .SingleAsync();

        if (mismatches != 0)
        {
            var sample = await db.Database
                .SqlQueryRaw<string>(
                    $@"SELECT (a.""AconexDocNo"" || ' | title=' || coalesce(a.""Title"", '')) AS ""Value""
                       FROM ""AconexRevisions"" a
                       WHERE a.""LineHash"" <> {hash}
                       LIMIT 3")
                .ToListAsync();
            throw new Xunit.Sdk.XunitException(
                $"{mismatches} rows differ between the migration's SQL and AconexLineHasher. "
                + $"Samples: {string.Join(" // ", sample)}");
        }
#pragma warning restore EF1002
    }

    // ------------------------------------------------------------------ helpers

    private async Task ResetAsync()
    {
        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        await db.AconexRevisions.Where(a => a.ProjectId == _fixture.QpacProjectId).ExecuteDeleteAsync();
        await db.ImportBatches.Where(b => b.ProjectId == _fixture.QpacProjectId).ExecuteDeleteAsync();
    }

    private async Task<ImportResult> ImportAsync(string fileName)
    {
        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var importer = scope.ServiceProvider.GetRequiredService<AconexHistoryImporter>();
        var batch = await NewBatchAsync(db, fileName);
        return await importer.ImportAsync(
            _fixture.QpacProjectId, SampleFiles.Open(fileName), batch, "test", CancellationToken.None);
    }

    private async Task<Guid> NewBatchAsync(DipDbContext db, string fileName)
    {
        var batch = new ImportBatch
        {
            ProjectId = _fixture.QpacProjectId,
            Kind = ImportKind.AconexHistory,
            FileName = fileName,
            ImportedAt = DateTime.UtcNow,
            UploadedBy = "test",
            Status = ImportBatchStatus.Completed,
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        return batch.Id;
    }

    private AconexRevision Revision(Guid batchId, string number, DateTime when, string title) => new()
    {
        ProjectId = _fixture.QpacProjectId,
        ImportBatchId = batchId,
        LineHash = AconexLineHasher.Compute("pdf", "f.pdf", number, "00", title, "Issued For Approval",
            null, when, null, null, null, null, null, null),
        FileType = "pdf",
        FileName = "f.pdf",
        AconexDocNo = number,
        DocNoFinal = number,
        Revision = "00",
        Title = title,
        AconexStatus = "Issued For Approval",
        DateModified = when,
    };

    private async Task<int> CountAsync()
    {
        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        return await db.AconexRevisions.CountAsync(a => a.ProjectId == _fixture.QpacProjectId);
    }

    private async Task<List<Guid>> BatchIdsAsync()
    {
        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        return await db.AconexRevisions.AsNoTracking()
            .Where(a => a.ProjectId == _fixture.QpacProjectId)
            .Select(a => a.ImportBatchId)
            .Distinct()
            .ToListAsync();
    }
}
