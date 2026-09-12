using Dip.Application.Authorization;
using Dip.Domain.Enums;
using Dip.Infrastructure;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dip.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class MigrationTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public MigrationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _fixture.ConnectionString,
                ["Seed:AdminEmail"] = "admin@dip.test",
                ["Seed:AdminPassword"] = "SuperSecret!23",
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Migration_Applies_And_Seeder_Populates()
    {
        SkipIfNoSqlServer.RequireConnection(_fixture);
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        await _fixture.EnsureMigratedAsync(db);

        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync();

        // Project seeded.
        var project = await db.Projects.SingleAsync(p => p.Code == SeedData.QpacProjectCode);
        project.Name.Should().Be("Qiddiya Performing Arts Center");

        // Disciplines seeded (10 rows including STL alias).
        var disciplineCount = await db.Disciplines.CountAsync(d => d.ProjectId == project.Id);
        disciplineCount.Should().Be(SeedData.Disciplines.Count);

        // Status mappings seeded (22 rows).
        var mappingCount = await db.StatusMappings.CountAsync(m => m.ProjectId == project.Id);
        mappingCount.Should().Be(SeedData.StatusMappings.Count);

        // The plural form ("Comments") maps to Approved.
        var pluralMapping = await db.StatusMappings.SingleAsync(m =>
            m.ProjectId == project.Id && m.AconexStatus == "B - Approved with Comments");
        pluralMapping.Status.Should().Be(UnifiedStatus.Approved);
        pluralMapping.IsLegacy.Should().BeFalse();

        // Legacy singular form is still present but flagged legacy.
        var singularMapping = await db.StatusMappings.SingleAsync(m =>
            m.ProjectId == project.Id && m.AconexStatus == "B - Approved with Comment");
        singularMapping.IsLegacy.Should().BeTrue();

        // Sequence widths seeded for the eight document types the sample MIDP uses;
        // every other type falls through to SerialWidths.DefaultWidth.
        var serials = await db.DocumentTypeSerials
            .Where(s => s.ProjectId == project.Id)
            .ToListAsync();
        serials.Should().HaveCount(SeedPicklists.SerialWidths.Count);
        serials.Single(s => s.DocType == "SDW").SequenceWidth.Should().Be(4);
        serials.Single(s => s.DocType == "CAL").SequenceWidth.Should().Be(3);

        // 5 roles, and SuperAdmin has all permissions.
        var superAdmin = await db.Roles.SingleAsync(r => r.Name == RoleDefinitions.SuperAdmin);
        var superAdminClaims = await db.RoleClaims
            .Where(c => c.RoleId == superAdmin.Id && c.ClaimType == "permission")
            .Select(c => c.ClaimValue)
            .ToListAsync();
        superAdminClaims.Should().BeEquivalentTo(Permissions.All);

        // Editor role has DraftsEdit but not ImportRun.
        var editor = await db.Roles.SingleAsync(r => r.Name == RoleDefinitions.Editor);
        var editorClaims = await db.RoleClaims
            .Where(c => c.RoleId == editor.Id && c.ClaimType == "permission")
            .Select(c => c.ClaimValue)
            .ToListAsync();
        editorClaims.Should().Contain(Permissions.DocumentsEdit);
        editorClaims.Should().NotContain(Permissions.ImportRun);

        // SuperAdmin user exists and is in the SuperAdmin role.
        var adminUser = await db.Users.SingleAsync(u => u.Email == "admin@dip.test");
        adminUser.IsActive.Should().BeTrue();
        var isInRole = await db.UserRoles
            .AnyAsync(ur => ur.UserId == adminUser.Id && ur.RoleId == superAdmin.Id);
        isInRole.Should().BeTrue();
    }

    [Fact]
    public async Task Seeder_Is_Idempotent()
    {
        SkipIfNoSqlServer.RequireConnection(_fixture);
        await using var provider = BuildProvider();
        using var scope1 = provider.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<DipDbContext>();
        await _fixture.EnsureMigratedAsync(db1);

        var seeder1 = scope1.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder1.SeedAsync();
        var projectCountAfterFirst = await db1.Projects.CountAsync();
        var mappingCountAfterFirst = await db1.StatusMappings.CountAsync();
        var roleCountAfterFirst = await db1.Roles.CountAsync();

        using var scope2 = provider.CreateScope();
        var seeder2 = scope2.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder2.SeedAsync();

        var db2 = scope2.ServiceProvider.GetRequiredService<DipDbContext>();
        (await db2.Projects.CountAsync()).Should().Be(projectCountAfterFirst);
        (await db2.StatusMappings.CountAsync()).Should().Be(mappingCountAfterFirst);
        (await db2.Roles.CountAsync()).Should().Be(roleCountAfterFirst);
    }
}
