using Dip.Api;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dip.Api.IntegrationTests;

// A local database built by EnsureCreated is never altered once the file exists, so a
// schema change lands as a request-time "no such column" on every page instead of as a
// startup failure. That is what these two facts are about: the stale file is rebuilt,
// and — just as important — a current file is left alone, because a guard that rebuilt
// on every start would silently empty the developer's database each time they ran.
public class LocalSqliteSchemaTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "dip-schema-" + Guid.NewGuid().ToString("N"));

    private string DatabasePath => Path.Combine(_directory, "dip-local.db");

    private DipDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<DipDbContext>()
            .UseSqlite($"Data Source={DatabasePath}")
            .Options;
        return new DipDbContext(options);
    }

    [Fact]
    public async Task AFileOlderThanTheModel_IsRebuiltAndKept()
    {
        Directory.CreateDirectory(_directory);

        // A file from before the model moved on: one table, none of the current columns.
        await using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Documents (Id TEXT NOT NULL);";
            await command.ExecuteNonQueryAsync();
        }
        SqliteConnection.ClearAllPools();

        await using (var db = NewContext())
        {
            await LocalSqliteSchema.EnsureCurrentAsync(db, DatabasePath);

            // The rebuilt file carries the whole current model, not the one stale table.
            var tables = await TableNamesAsync(db);
            tables.Should().Contain("TidpFiles");
            tables.Should().Contain("AuditLogs");
        }

        Directory.GetFiles(_directory, "dip-local.db.stale-*").Should().ContainSingle(
            "the previous database is moved aside, never deleted");
    }

    [Fact]
    public async Task ACurrentFile_IsLeftAlone()
    {
        Directory.CreateDirectory(_directory);

        await using (var db = NewContext())
        {
            await LocalSqliteSchema.EnsureCurrentAsync(db, DatabasePath);
            db.Projects.Add(new Domain.Entities.Project { Name = "Kept", Code = "KEEP" });
            await db.SaveChangesAsync();
        }
        SqliteConnection.ClearAllPools();

        await using (var db = NewContext())
        {
            await LocalSqliteSchema.EnsureCurrentAsync(db, DatabasePath);

            (await db.Projects.CountAsync()).Should().Be(1,
                "a second start must not empty a database whose schema is current");
        }

        Directory.GetFiles(_directory, "dip-local.db.stale-*").Should().BeEmpty();
    }

    private static async Task<List<string>> TableNamesAsync(DipDbContext db)
    {
        var names = new List<string>();
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));

        return names;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
