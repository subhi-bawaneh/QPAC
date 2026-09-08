using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Dip.Infrastructure.Tests;

// Reads a Postgres connection string from env var TEST_POSTGRES_CONNECTION and
// isolates each test run inside its own schema (`dip_test_{guid}`) so multiple
// runs don't clash even against a shared Neon branch.
//
// If the env var isn't set, tests using this fixture skip themselves via
// SkipIfNoPostgres.RequireConnection() so contributors without a DB don't fail
// the whole suite.
//
// Recommended setup:
//   1. Run a local scratch Postgres — never Neon, which TestConnectionGuard refuses.
//   2. Export TEST_POSTGRES_CONNECTION="Host=localhost;Port=5432;Database=dip_test;Username=postgres;Password=postgres"
//      before running dotnet test.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string? _configuredConnectionString;
    private readonly string _schema;

    public PostgresFixture()
    {
        _configuredConnectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        TestConnectionGuard.Assert(_configuredConnectionString);
        _schema = "dip_test_" + Guid.NewGuid().ToString("N")[..12];
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_configuredConnectionString);

    public string ConnectionString => IsAvailable
        ? $"{_configuredConnectionString};Search Path={_schema}"
        : throw new InvalidOperationException(
            "TEST_POSTGRES_CONNECTION env var is not set. Point it at a Neon branch or a scratch database.");

    public string Schema => _schema;

    // EF resolves __EFMigrationsHistory against the model's default schema ("public"),
    // while the migration script itself creates unqualified tables into the search path
    // (our per-run schema). The applied-migrations check therefore always comes back
    // empty and a second MigrateAsync on the same schema re-runs the whole script and
    // fails with 42P07. Tests share one schema per class fixture, so migrate exactly once.
    private readonly SemaphoreSlim _migrationGate = new(1, 1);
    private bool _migrated;

    public async Task EnsureMigratedAsync(DbContext db, CancellationToken ct = default)
    {
        await _migrationGate.WaitAsync(ct);
        try
        {
            if (_migrated) return;
            await db.Database.MigrateAsync(ct);
            _migrated = true;
        }
        finally
        {
            _migrationGate.Release();
        }
    }

    public async Task InitializeAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_configuredConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{_schema}\"";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_configuredConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE";
        await cmd.ExecuteNonQueryAsync();
    }
}

// Sentinel exception used by tests that need Postgres. When thrown, the test
// runner still marks the test Failed, but the message tells the developer
// exactly what to set. We prefer this over silently skipping because it's
// visible in CI. Set TEST_POSTGRES_SKIP_MISSING=1 to convert it to a pass.
internal static class SkipIfNoPostgres
{
    public static void RequireConnection(PostgresFixture fixture)
    {
        if (fixture.IsAvailable)
        {
            return;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("TEST_POSTGRES_SKIP_MISSING"), "1", StringComparison.Ordinal))
        {
            throw new IgnoreException("TEST_POSTGRES_CONNECTION not set; skipping.");
        }

        throw new InvalidOperationException(
            "TEST_POSTGRES_CONNECTION env var is not set. Set it to a Neon branch or scratch database " +
            "to run integration tests, or set TEST_POSTGRES_SKIP_MISSING=1 to skip them locally.");
    }
}

// Small helper so tests can bail without failing the build in dev-only setups.
public sealed class IgnoreException : Exception
{
    public IgnoreException(string message) : base(message) { }
}
