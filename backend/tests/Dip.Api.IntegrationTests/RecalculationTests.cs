using Dip.Api.Features.Recalculation;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Snapshots are the materialised tracker rows: they carry the display columns copied
// off the document, and a document that goes takes its snapshot with it — by foreign
// key now, rather than by the sweep the two-layer model needed.
[Collection(IntegrationTestCollection.Name)]
public class RecalculationTests
{
    private readonly DipApiFactory _factory;

    public RecalculationTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Documents_GetSnapshots_CarryingTheDisplayColumns()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var projectId = await TestHelpers.NewProjectAsync(_factory, "Recalculation test");
        var (tidpFileId, documentIds) = await TestHelpers.SeedDocumentsAsync(_factory, projectId, count: 4);

        await RunAllAsync(projectId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var snapshots = await db.DocumentSnapshots.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .ToListAsync();

        snapshots.Should().HaveCount(4);
        snapshots.Should().OnlyContain(s => s.DocumentNumber != "");
        snapshots.Should().OnlyContain(s => s.Discipline == "Structural");
        snapshots.Should().OnlyContain(s => s.Trade == "STL");
        snapshots.Should().OnlyContain(s => s.TidpFileId == tidpFileId);
        snapshots.Select(s => s.DocumentId).Should().BeEquivalentTo(documentIds);
    }

    // The foreign key is what removes a stale row now. Deleting a document must take
    // its tracker row with it, or the register would keep showing a drawing that the
    // replace or delete path has already removed.
    [Fact]
    public async Task DeletingADocument_RemovesItsSnapshot()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var projectId = await TestHelpers.NewProjectAsync(_factory, "Snapshot cascade test");
        var (_, documentIds) = await TestHelpers.SeedDocumentsAsync(_factory, projectId, count: 3);

        await RunAllAsync(projectId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            (await db.DocumentSnapshots.CountAsync(s => s.ProjectId == projectId)).Should().Be(3);

            await db.Documents.Where(d => d.Id == documentIds[0]).ExecuteDeleteAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            var remaining = await db.DocumentSnapshots.AsNoTracking()
                .Where(s => s.ProjectId == projectId)
                .Select(s => s.DocumentId)
                .ToListAsync();

            remaining.Should().HaveCount(2);
            remaining.Should().NotContain(documentIds[0]);
        }
    }

    private async Task RunAllAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RecalculationService>()
            .RunAllAsync(projectId, CancellationToken.None);
    }
}
