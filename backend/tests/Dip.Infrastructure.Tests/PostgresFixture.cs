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
//   1. Create a dedicated Neon *branch* or *database* — never point tests at prod.
//   2. Export TEST_POSTGRES_CONNECTION="Host=...;Database=...;Username=...;Password=...;SSL Mode=Require"
//      before running dotnet test.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string? _configuredConnectionString;
    private readonly string _schema;

    public PostgresFixture()
    {
        _configuredConnectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        _schema = "dip_test_" + Guid.NewGuid().ToString("N")[..12];
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_configuredConnectionString);

    public string ConnectionString => IsAvailable
        ? $"{_configuredConnectionString};Search Path={_schema}"
        : throw new InvalidOperationException(
            "TEST_POSTGRES_CONNECTION env var is not set. Point it at a Neon branch or a scratch database.");

    public string Schema => _schema;

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
