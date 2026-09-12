using Microsoft.Extensions.Configuration;

namespace Dip.Infrastructure.Persistence;

// Which relational engine the context is talking to. Production is always SQL Server;
// Sqlite exists so a developer can run the whole system — API, workers, frontend — on
// one file with no server, no Docker and no cloud database. See docs/local-dev.md.
public enum DatabaseProvider
{
    SqlServer,
    Sqlite,
}

public static class DatabaseProviderResolver
{
    public const string ConfigKey = "Database:Provider";

    /// Reads Database:Provider, falling back to what the connection string looks like.
    public static DatabaseProvider Resolve(IConfiguration configuration)
    {
        var configured = configuration[ConfigKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim().ToLowerInvariant() switch
            {
                "sqlite" or "sqlite3" => DatabaseProvider.Sqlite,
                "sqlserver" or "mssql" => DatabaseProvider.SqlServer,
                _ => throw new InvalidOperationException(
                    $"Unknown {ConfigKey} '{configured}'. Use 'SqlServer' or 'Sqlite'."),
            };
        }

        return Infer(configuration.GetConnectionString("Default"));
    }

    /// A SQLite connection string names only a file ("Data Source=foo.db"); a SQL Server
    /// one always carries a login or catalog alongside the server name.
    public static DatabaseProvider Infer(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return DatabaseProvider.SqlServer;

        var value = connectionString.ToLowerInvariant();
        if (value.EndsWith(".db", StringComparison.Ordinal)
            || value.EndsWith(".sqlite", StringComparison.Ordinal)
            || value.EndsWith(".sqlite3", StringComparison.Ordinal))
        {
            return DatabaseProvider.Sqlite;
        }

        if (value.Contains("data source=")
            && !value.Contains("user id=")
            && !value.Contains("initial catalog=")
            && !value.Contains("password="))
        {
            return DatabaseProvider.Sqlite;
        }

        return DatabaseProvider.SqlServer;
    }
}
