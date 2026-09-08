using Dip.Domain.Enums;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class PicklistImporterTests : IClassFixture<ImporterFixture>
{
    private readonly ImporterFixture _fixture;

    public PicklistImporterTests(ImporterFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ImportsRealPicklistsFile_AndPopulatesEveryDeclaredField()
    {
        if (!_fixture.IsAvailable) return;

        using var scope = _fixture.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<PicklistImporter>();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var result = await importer.ImportAsync(
            _fixture.QpacProjectId,
            SampleFiles.Open("PickLists.xlsx"),
            CancellationToken.None);

        result.RowsRead.Should().BeGreaterThan(50, "PickLists.xlsx has dozens of codes across many FIELD columns");

        // A well-known code: FIELD 05 - DISCIPLINE - "STL" -> "Steel" or "Structural" depending on the source.
        // The important assertion is that the code exists at all.
        var stlItem = await db.PicklistItems
            .SingleOrDefaultAsync(p =>
                p.ProjectId == _fixture.QpacProjectId
                && p.Field == PicklistField.Discipline
                && p.Code == "STL");
        stlItem.Should().NotBeNull("STL is a canonical discipline code in the sample");

        // FIELD 04 - DOCUMENT TYPES - SDW (Shop Drawing) is used throughout the sample.
        var sdw = await db.PicklistItems.SingleOrDefaultAsync(p =>
            p.ProjectId == _fixture.QpacProjectId && p.Field == PicklistField.DocType && p.Code == "SDW");
        sdw.Should().NotBeNull("SDW (Shop Drawing) should always be present in FIELD 04");
    }
}
