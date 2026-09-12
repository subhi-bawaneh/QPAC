using System.Security.Cryptography;
using System.Text;
using Dip.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Serilog;

namespace Dip.Api;

// The local file is built by EnsureCreated, which does nothing at all when the file
// already exists. Change an entity and the old schema survives untouched, so the API
// starts cleanly and then every page that reads the changed table fails with
// "no such column" — a startup problem reported as a hundred request failures.
//
// So: fingerprint the model, keep the fingerprint inside the file, and rebuild the file
// when the two disagree. The stale file is moved aside rather than deleted, because a
// local database is cheap to rebuild but nobody wants it silently destroyed.
internal static class LocalSqliteSchema
{
    private const string FingerprintTable = "__DipLocalSchema";

    public static async Task EnsureCurrentAsync(DipDbContext db, string dataSource)
    {
        var path = Path.GetFullPath(dataSource);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var wanted = Fingerprint(db.Model);

        if (File.Exists(path) && ReadFingerprint(path) != wanted)
        {
            var moved = SetAside(path);
            Log.Warning(
                "The local SQLite schema no longer matches the model, so it has been "
                + "rebuilt empty. The previous file is at {Stale}. Re-upload the TIDPs, "
                + "the baseline, the picklists and the Aconex export.", moved);
        }

        await db.Database.EnsureCreatedAsync();

        // The import and sync workers write while the API serves requests; without WAL
        // the default rollback journal locks readers out and they fail with SQLITE_BUSY.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");

        await WriteFingerprintAsync(db, wanted);
    }

    // Tables and their columns: the shape whose drift produces "no such column" and
    // "no such table". Indexes and keys are left out deliberately — they do not break a
    // running query, and including them would rebuild the file over cosmetic changes.
    private static string Fingerprint(IModel model)
    {
        var text = new StringBuilder();

        foreach (var entity in model.GetEntityTypes().OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            var table = entity.GetTableName();
            if (table is null) continue;

            var id = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var columns = entity.GetProperties()
                .Select(p => $"{p.GetColumnName(id)}:{p.GetColumnType()}:{(p.IsNullable ? '?' : '!')}")
                .OrderBy(c => c, StringComparer.Ordinal);

            text.Append(table).Append('(').AppendJoin(',', columns).Append(");");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static string? ReadFingerprint(string path)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={path}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT Fingerprint FROM {FingerprintTable} LIMIT 1;";
            return command.ExecuteScalar() as string;
        }
        catch (SqliteException)
        {
            // No such table: a file written before this check existed, or a corrupt one.
            // Either way the schema cannot be trusted.
            return null;
        }
    }

    private static async Task WriteFingerprintAsync(DipDbContext db, string fingerprint)
    {
        await db.Database.ExecuteSqlRawAsync(
            $"CREATE TABLE IF NOT EXISTS {FingerprintTable} (Fingerprint TEXT NOT NULL);");
        await db.Database.ExecuteSqlRawAsync($"DELETE FROM {FingerprintTable};");
        await db.Database.ExecuteSqlRawAsync(
            $"INSERT INTO {FingerprintTable} (Fingerprint) VALUES ({{0}});", fingerprint);
    }

    // The connection pool holds the file open, so it has to be emptied before the move.
    private static string SetAside(string path)
    {
        SqliteConnection.ClearAllPools();

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var target = $"{path}.stale-{stamp}";

        // The write-ahead log travels with the database under the matching name, so the
        // kept copy still opens with its last transactions intact. Dropping it here would
        // leave a file that is set aside but quietly short of what it held.
        File.Move(path, target, overwrite: true);
        foreach (var sidecar in new[] { "-wal", "-shm" })
        {
            if (File.Exists(path + sidecar)) File.Move(path + sidecar, target + sidecar, overwrite: true);
        }

        return target;
    }
}
