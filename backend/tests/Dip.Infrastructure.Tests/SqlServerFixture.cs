using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dip.Infrastructure.Tests;

// Reads a SQL Server connection string from env var TEST_SQLSERVER_CONNECTION and
// isolates each test run inside its own throwaway database (`dip_test_{guid}`) so
// multiple runs don't clash against a shared local instance.
//
// If the env var isn't set, tests using this fixture skip themselves via
// SkipIfNoSqlServer.RequireConnection() so contributors without a DB don't fail
// the whole suite.
//
// Recommended setup:
//   1. Run a local scratch SQL Server — never the production ASPMonster instance,
//      which TestConnectionGuard refuses — e.g.
//      docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=Test_1234 -p 1433:1433 \
//        mcr.microsoft.com/mssql/server:2022-latest
//   2. Export TEST_SQLSERVER_CONNECTION="Server=localhost;User Id=sa;Password=Test_1234;TrustServerCertificate=true"
//      (no Database= — one is created per run) before running dotnet test.
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly string? _configuredConnectionString;
    private readonly string _database;

    public SqlServerFixture()
    {
        _configuredConnectionString = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION");
        TestConnectionGuard.Assert(_configuredConnectionString);
        _database = "dip_test_" + Guid.NewGuid().ToString("N")[..12];
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_configuredConnectionString);

    public string ConnectionString => IsAvailable
        ? $"{_configuredConnectionString};Database={_database}"
        : throw new InvalidOperationException(
            "TEST_SQLSERVER_CONNECTION env var is not set. Point it at a local scratch SQL Server.");

    public string Database => _database;

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
            throw new InvalidOperationException(
                "TEST_SQLSERVER_CONNECTION is not set. Point it at a local scratch SQL Server "
                + "(never the production ASPMonster instance) before running the database tests.");
        }

        await using var conn = new SqlConnection(_configuredConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{_database}]";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        await using var conn = new SqlConnection(_configuredConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        // Kick any lingering connection off the database before dropping it — the
        // fixture's own pooled connections from the run just ended can otherwise
        // block the DROP with "database is in use".
        cmd.CommandText =
            $"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
            + $"DROP DATABASE [{_database}]";
        await cmd.ExecuteNonQueryAsync();
    }
}

// Sentinel exception used by tests that need SQL Server. When thrown, the test
// runner still marks the test Failed, but the message tells the developer
// exactly what to set. We prefer this over silently skipping because it's
// visible in CI. Set TEST_SQLSERVER_SKIP_MISSING=1 to convert it to a pass.
internal static class SkipIfNoSqlServer
{
    public static void RequireConnection(SqlServerFixture fixture)
    {
        if (fixture.IsAvailable)
        {
            return;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("TEST_SQLSERVER_SKIP_MISSING"), "1", StringComparison.Ordinal))
        {
            throw new IgnoreException("TEST_SQLSERVER_CONNECTION not set; skipping.");
        }

        throw new InvalidOperationException(
            "TEST_SQLSERVER_CONNECTION env var is not set. Set it to a local scratch SQL Server " +
            "to run integration tests, or set TEST_SQLSERVER_SKIP_MISSING=1 to skip them locally.");
    }
}

// Small helper so tests can bail without failing the build in dev-only setups.
public sealed class IgnoreException : Exception
{
    public IgnoreException(string message) : base(message) { }
}
