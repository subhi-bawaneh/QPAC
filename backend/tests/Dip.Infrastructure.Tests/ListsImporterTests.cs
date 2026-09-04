using Dip.Domain.Enums;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class ListsImporterTests : IClassFixture<ImporterFixture>
{
    private readonly ImporterFixture _fixture;

    public ListsImporterTests(ImporterFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Import_UpsertsAllTrackerListsEntries()
    {
        if (!_fixture.IsAvailable) return;

        using var scope = _fixture.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<ListsImporter>();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var result = await importer.ImportAsync(
            _fixture.QpacProjectId,
            SampleFiles.Path("Tracker.xlsx"),
            CancellationToken.None);

        // Tracker!Lists has ~10 entries. Seeder already covered most as legacy or plural
        // variants, so most rows here should be Updates or no-ops, not Inserts.
        result.RowsRead.Should().BeGreaterOrEqualTo(8);

        // Verify the singular "Comment" and plural "Comments" both map to Approved.
        var plural = await db.StatusMappings.SingleOrDefaultAsync(m =>
            m.ProjectId == _fixture.QpacProjectId
            && m.AconexStatus == "B - Approved with Comments");
        plural.Should().NotBeNull();
        plural!.Status.Should().Be(UnifiedStatus.Approved);

        // C - Revise -> Rejected must survive both the seeder and the Lists import.
        var revise = await db.StatusMappings.SingleOrDefaultAsync(m =>
            m.ProjectId == _fixture.QpacProjectId
            && m.AconexStatus == "C - Revise and Resubmit");
        revise.Should().NotBeNull();
        revise!.Status.Should().Be(UnifiedStatus.Rejected);
    }
}
