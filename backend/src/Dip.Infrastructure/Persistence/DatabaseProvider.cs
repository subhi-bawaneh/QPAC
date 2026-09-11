using Microsoft.Extensions.Configuration;

namespace Dip.Infrastructure.Persistence;

// Which relational engine the context is talking to. Production is always Postgres
// (Neon); Sqlite exists so a developer can run the whole system — API, workers,
// frontend — on one file with no server, no Docker and no Neon branch.
// See docs/local-dev.md.
public enum DatabaseProvider
{
    Postgres,
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
                "postgres" or "postgresql" or "npgsql" => DatabaseProvider.Postgres,
                _ => throw new InvalidOperationException(
                    $"Unknown {ConfigKey} '{configured}'. Use 'Postgres' or 'Sqlite'."),
            };
        }

        return Infer(configuration.GetConnectionString("Default"));
    }

    /// A Postgres connection string names a Host/Server; a SQLite one names a file.
    public static DatabaseProvider Infer(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return DatabaseProvider.Postgres;

        var value = connectionString.ToLowerInvariant();
        if (value.Contains("host=") || value.Contains("server=")) return DatabaseProvider.Postgres;
        if (value.Contains("data source=")
            || value.EndsWith(".db", StringComparison.Ordinal)
            || value.EndsWith(".sqlite", StringComparison.Ordinal)
            || value.EndsWith(".sqlite3", StringComparison.Ordinal))
        {
            return DatabaseProvider.Sqlite;
        }

        return DatabaseProvider.Postgres;
    }
}
