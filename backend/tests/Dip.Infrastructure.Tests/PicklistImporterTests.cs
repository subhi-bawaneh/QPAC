using Dip.Domain.Enums;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Infrastructure.Tests;

// Every list in samples/PickLists.xlsx — the 17 of refactor-plan § 7 — with the row
// counts and first values verified against the workbook on 2026-09-08.
[Trait("Category", "Integration")]
public class PicklistImporterTests : IClassFixture<ImporterFixture>
{
    private readonly ImporterFixture _fixture;

    public PicklistImporterTests(ImporterFixture fixture) => _fixture = fixture;

    // (field, row count, first code, first description)
    public static TheoryData<PicklistField, int, string, string> Expected() => new()
    {
        { PicklistField.Project, 1, "QF01012", "Qiddaya Performing Arts Center" },
        { PicklistField.Originator, 1, "NES", "Nesma and Partners" },
        { PicklistField.Contract, 1, "C04518", "Contract Number" },
        { PicklistField.DocType, 94, "AGD", "Agenda" },
        { PicklistField.Discipline, 98, "ACO", "Acoustic" },
        { PicklistField.Zone, 29, "00", "Overall" },
        { PicklistField.Building, 12, "Z00000", "Overall" },
        { PicklistField.DrawingType, 10, "0", "General (General Notes, Abbreviations, Legends, Single Line Diagrams, General Detail)" },
        { PicklistField.Level, 16, "00", "Non-Specific ( For Blade )" },
        { PicklistField.AuthoringSoftware, 13, "Revit", "" },
        { PicklistField.ExchangeFormat, 26, ".rvt", "" },
        { PicklistField.ScopeArea, 31, "Design Management", "" },
        { PicklistField.SuitabilityCode, 14, "S0 (WIP)", "Initial status or WIP" },
        { PicklistField.Scale, 16, "NTS", "" },
        { PicklistField.Classification, 10, "FI_60_25", "Drawing" },
        { PicklistField.CorporateDiscipline, 9, "Architectural", "" },
        { PicklistField.Author, 12, "Nesma & Partners", "" },
    };

    [Fact]
    public async Task ImportsEveryListInTheWorkbook()
    {
        if (!_fixture.IsAvailable) return;

        var items = await ImportAsync();

        foreach (var row in Expected())
        {
            var field = (PicklistField)row[0]!;
            var count = (int)row[1]!;
            var firstCode = (string)row[2]!;
            var firstDescription = (string)row[3]!;

            var list = items.Where(i => i.Field == field).OrderBy(i => i.SortOrder).ToList();
            list.Should().HaveCount(count, "list {0} has {1} rows in the workbook", field, count);
            list[0].Code.Should().Be(firstCode);
            list[0].Description.Should().Be(firstDescription);
        }

        // The 8C column holds prose, not codes, and is deliberately not a list.
        items.Select(i => i.Field).Distinct().Should().HaveCount(Expected().Count);
    }

    [Fact]
    public async Task PreservesLeadingZeros_AndTrimsTrailingSpaces()
    {
        if (!_fixture.IsAvailable) return;

        var items = await ImportAsync();

        items.Should().Contain(i => i.Field == PicklistField.Zone && i.Code == "00");
        items.Should().Contain(i => i.Field == PicklistField.Level && i.Code == "00");
        items.Where(i => i.Field == PicklistField.ScopeArea)
            .Should().OnlyContain(i => i.Code == i.Code.Trim());
        items.Single(i => i.Field == PicklistField.ScopeArea && i.SortOrder == 1)
            .Code.Should().Be("Design Management");
    }

    [Fact]
    public async Task ReImportSkipsSoftDeletedCodes()
    {
        if (!_fixture.IsAvailable) return;

        await ImportAsync();

        using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            var tke = await db.PicklistItems.SingleAsync(p =>
                p.ProjectId == _fixture.QpacProjectId
                && p.Field == PicklistField.Author
                && p.Code == "TKE");
            tke.IsDeleted = true;
            tke.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        using var second = _fixture.CreateScope();
        var importer = second.ServiceProvider.GetRequiredService<PicklistImporter>();
        var result = await importer.ImportAsync(
            _fixture.QpacProjectId, SampleFiles.Open("PickLists.xlsx"), CancellationToken.None);

        result.RowsSkipped.Should().Be(1);
        result.Warnings.Should().Contain(w => w.Contains("TKE", StringComparison.Ordinal));

        var db2 = second.ServiceProvider.GetRequiredService<DipDbContext>();
        var restored = await db2.PicklistItems.SingleAsync(p =>
            p.ProjectId == _fixture.QpacProjectId
            && p.Field == PicklistField.Author
            && p.Code == "TKE");
        restored.IsDeleted.Should().BeTrue("a re-import must not resurrect a deleted code");
    }

    private async Task<List<Domain.Entities.PicklistItem>> ImportAsync()
    {
        using var scope = _fixture.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<PicklistImporter>();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        await db.PicklistItems.Where(p => p.ProjectId == _fixture.QpacProjectId).ExecuteDeleteAsync();

        var result = await importer.ImportAsync(
            _fixture.QpacProjectId, SampleFiles.Open("PickLists.xlsx"), CancellationToken.None);
        result.RowsInserted.Should().Be(result.RowsRead);

        return await db.PicklistItems
            .AsNoTracking()
            .Where(p => p.ProjectId == _fixture.QpacProjectId)
            .ToListAsync();
    }
}
