using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Real-SQL-Server factory with per-run database isolation.
// - Reads TEST_SQLSERVER_CONNECTION from env; skips if unset.
// - Creates database `dip_int_{guid12}` and points the connection string at it.
// - Runs migrations + seeder against the isolated database.
// - Seeds a well-known SuperAdmin (Seed:AdminEmail / Seed:AdminPassword) plus test users.
// - Drops the database on dispose.
public sealed class DipApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SuperAdminEmail = "superadmin@dip.test";
    public const string SuperAdminPassword = "IntegrationTest!23";

    private readonly string? _baseConnectionString;
    private readonly string _database;

    public DipApiFactory()
    {
        _baseConnectionString = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION");
        Dip.Infrastructure.Tests.TestConnectionGuard.Assert(_baseConnectionString);
        _database = "dip_int_" + Guid.NewGuid().ToString("N")[..12];
    }

    public bool IsSqlServerAvailable => !string.IsNullOrWhiteSpace(_baseConnectionString);

    public string ConnectionString => IsSqlServerAvailable
        ? $"{_baseConnectionString};Database={_database}"
        : throw new InvalidOperationException("TEST_SQLSERVER_CONNECTION not set");

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // The suite is a SQL Server suite. Without this the Development-only local
            // defaults would redirect the host onto a SQLite file (see LocalDevDefaults).
            ["Database:Provider"] = "SqlServer",
            // If SQL Server isn't available, use a placeholder so the host still starts
            // (tests that hit real endpoints will be skipped anyway).
            ["ConnectionStrings:Default"] = IsSqlServerAvailable
                ? ConnectionString
                : "Server=localhost;Database=dip_placeholder;User Id=x;Password=x",
            ["Jwt:Key"] = "integration-test-key-not-secret-32bytes-min!",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7",
            ["Seed:AdminEmail"] = SuperAdminEmail,
            ["Seed:AdminPassword"] = SuperAdminPassword,
            // Tests own the DB lifecycle via InitializeAsync — the app must not migrate.
            ["Startup:SkipMigration"] = "true",
            // Every test in the collection logs in through the same in-memory client, so
            // they all land in one rate-limit partition and would exhaust the production
            // window between them. AuthRateLimitTests boots its own host with a small
            // limit to prove the 429; here the limiter must never be the thing that fails.
            ["RateLimit:AuthPermitPerWindow"] = "100000",
            // No Drive polling in tests: the sync service is exercised directly with a
            // fake IDriveClient. The import worker stays on — the flow tests wait on it.
            ["GoogleDrive:PollHours"] = "0",
            ["GoogleDrive:StartupDelaySeconds"] = "0",
        }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<Dip.Api.Hubs.ISyncNotifier>();
            services.AddSingleton<Dip.Api.Hubs.ISyncNotifier, Dip.Api.Hubs.NoopSyncNotifier>();
        });

        return base.CreateHost(builder);
    }

    public async Task InitializeAsync()
    {
        if (!IsSqlServerAvailable)
        {
            // A green run with every integration test skipped proves nothing
            // (refactor-plan § 11.3), so the absence of a database is a failure.
            throw new InvalidOperationException(
                "TEST_SQLSERVER_CONNECTION is not set. Point it at a local scratch SQL Server "
                + "(never the production ASPMonster instance) before running the integration tests.");
        }

        // Create the isolated database before the host boots.
        await using (var conn = new SqlConnection(_baseConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{_database}]";
            await cmd.ExecuteNonQueryAsync();
        }

        // Boot the host once (via CreateClient) so DipDbContext resolves against
        // the isolated database, then run migrations + seeder.
        _ = CreateClient();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        await db.Database.MigrateAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (!IsSqlServerAvailable)
        {
            return;
        }

        await using var conn = new SqlConnection(_baseConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
            + $"DROP DATABASE [{_database}]";
        await cmd.ExecuteNonQueryAsync();
    }
}
