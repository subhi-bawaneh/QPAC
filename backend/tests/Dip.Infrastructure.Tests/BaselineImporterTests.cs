using Dip.Domain.Enums;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class BaselineImporterTests : IClassFixture<ImporterFixture>
{
    private readonly ImporterFixture _fixture;

    public BaselineImporterTests(ImporterFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ImportsRealBaselineFile_AndMatchesPlanTargetActivity()
    {
        if (!_fixture.IsAvailable) return;

        using var scope = _fixture.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<BaselineImporter>();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var result = await importer.ImportAsync(
            _fixture.QpacProjectId,
            SampleFiles.Open("Baseline.xlsx"),
            replace: true,
            CancellationToken.None);

        result.RowsRead.Should().BeGreaterThan(1000, "the sample baseline has ~1,375 activities");
        result.RowsInserted.Should().BeGreaterThan(1000);

        // PLAN.md § 9.3.1 verification: QP.M.GN.GEN.GEN.1400 is a Submittal ending 2025-10-30.
        var target = await db.BaselineActivities
            .SingleOrDefaultAsync(b => b.ProjectId == _fixture.QpacProjectId && b.ActivityCode == "QP.M.GN.GEN.GEN.1400");

        target.Should().NotBeNull("PLAN.md test target activity must exist in the imported baseline");
        target!.Type.Should().Be(BaselineActivityType.Submittal);
        target.Finish.Date.Should().Be(new DateTime(2025, 10, 30));
    }

    [Fact]
    public async Task Idempotent_SecondImportProducesZeroInserts()
    {
        if (!_fixture.IsAvailable) return;

        using var scope = _fixture.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<BaselineImporter>();

        await importer.ImportAsync(
            _fixture.QpacProjectId, SampleFiles.Open("Baseline.xlsx"),
            replace: false, CancellationToken.None);

        var second = await importer.ImportAsync(
            _fixture.QpacProjectId, SampleFiles.Open("Baseline.xlsx"),
            replace: false, CancellationToken.None);

        second.RowsInserted.Should().Be(0);
        second.RowsRead.Should().BeGreaterThan(1000);
        // Unchanged rows produce zero updates because the importer compares field-by-field.
        second.RowsUpdated.Should().Be(0);
    }
}
