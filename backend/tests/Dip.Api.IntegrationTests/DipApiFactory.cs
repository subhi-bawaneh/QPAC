using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Real-Postgres factory with per-run schema isolation.
// - Reads TEST_POSTGRES_CONNECTION from env; skips if unset.
// - Creates schema `dip_int_{guid12}` and appends `Search Path=` to the connection string.
// - Runs migrations + seeder against the isolated schema.
// - Seeds a well-known SuperAdmin (Seed:AdminEmail / Seed:AdminPassword) plus test users.
// - Drops the schema on dispose.
public sealed class DipApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SuperAdminEmail = "superadmin@dip.test";
    public const string SuperAdminPassword = "IntegrationTest!23";

    private readonly string? _baseConnectionString;
    private readonly string _schema;

    public DipApiFactory()
    {
        _baseConnectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        Dip.Infrastructure.Tests.TestConnectionGuard.Assert(_baseConnectionString);
        _schema = "dip_int_" + Guid.NewGuid().ToString("N")[..12];
    }

    public bool IsPostgresAvailable => !string.IsNullOrWhiteSpace(_baseConnectionString);

    public string ConnectionString => IsPostgresAvailable
        ? $"{_baseConnectionString};Search Path={_schema}"
        : throw new InvalidOperationException("TEST_POSTGRES_CONNECTION not set");

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // If Postgres isn't available, use a placeholder so the host still starts
            // (tests that hit real endpoints will be skipped anyway).
            ["ConnectionStrings:Default"] = IsPostgresAvailable
                ? ConnectionString
                : "Host=localhost;Database=dip_placeholder;Username=x;Password=x",
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
        if (!IsPostgresAvailable)
        {
            // A green run with every integration test skipped proves nothing
            // (refactor-plan § 11.3), so the absence of a database is a failure.
            throw new InvalidOperationException(
                "TEST_POSTGRES_CONNECTION is not set. Point it at the local scratch Postgres "
                + "(never Neon) before running the integration tests.");
        }

        // Create the isolated schema before the host boots.
        await using (var conn = new NpgsqlConnection(_baseConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{_schema}\"";
            await cmd.ExecuteNonQueryAsync();
        }

        // Boot the host once (via CreateClient) so DipDbContext resolves against
        // the isolated schema, then run migrations + seeder.
        _ = CreateClient();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        await db.Database.MigrateAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (!IsPostgresAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_baseConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE";
        await cmd.ExecuteNonQueryAsync();
    }
}
