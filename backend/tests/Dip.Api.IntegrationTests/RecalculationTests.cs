using Dip.Api.Features.Recalculation;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Snapshots are the materialised tracker rows (decision D11): they carry the layer and
// the display columns, and a row that leaves the effective set loses its snapshot.
[Collection(IntegrationTestCollection.Name)]
public class RecalculationTests
{
    private readonly DipApiFactory _factory;

    public RecalculationTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DraftRows_GetSnapshots_CarryingLayerAndDisplayColumns()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Recalculation test");
        var folderId = await TestHelpers.CreateFolderAsync(
            admin, $"RecalcDraft-{Guid.NewGuid():N}", projectId: projectId);
        await TestHelpers.SetTargetAsync(admin, folderId, "Draft");
        await TestHelpers.ImportedAsync(admin, folderId, "TIDP-STL.xlsx");

        await RunAllAsync(projectId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var snapshots = await db.DocumentSnapshots.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .ToListAsync();

        snapshots.Should().NotBeEmpty();
        snapshots.Should().OnlyContain(s => s.Layer == DataTarget.Draft);
        snapshots.Should().OnlyContain(s => s.DocumentNumber != "");
        snapshots.Should().OnlyContain(s => s.Discipline == "Structural");
        snapshots.Should().OnlyContain(s => s.Trade == "STL");

        // Every snapshot is keyed by a draft row id, not by a Live document id.
        var draftIds = await db.DocumentDrafts.AsNoTracking()
            .Where(d => d.ProjectId == projectId).Select(d => d.Id).ToListAsync();
        snapshots.Select(s => s.DocumentId).Should().BeSubsetOf(draftIds);
    }

    [Fact]
    public async Task StaleSnapshots_AreRemovedWhenTheRowLeavesTheEffectiveSet()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Stale snapshot test");
        var folderId = await TestHelpers.CreateFolderAsync(
            admin, $"RecalcStale-{Guid.NewGuid():N}", projectId: projectId);
        await TestHelpers.SetTargetAsync(admin, folderId, "Draft");
        var file = await TestHelpers.ImportedAsync(admin, folderId, "TIDP-STL.xlsx");
        var fileId = file.GetProperty("id").GetGuid();

        await RunAllAsync(projectId);

        Guid removedId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            var draft = await db.DocumentDrafts.FirstAsync(d => d.FolderFileId == fileId);
            removedId = draft.Id;
            db.DocumentDrafts.Remove(draft);
            await db.SaveChangesAsync();
        }

        await RunAllAsync(projectId);

        using var check = _factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<DipDbContext>();
        (await checkDb.DocumentSnapshots.AnyAsync(s => s.DocumentId == removedId))
            .Should().BeFalse("the row is no longer in the effective set");
    }

    private async Task RunAllAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RecalculationService>()
            .RunAllAsync(projectId, CancellationToken.None);
    }
}
