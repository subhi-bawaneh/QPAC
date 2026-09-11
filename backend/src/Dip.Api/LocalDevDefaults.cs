using System.Security.Cryptography;
using System.Text.Json;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api;

/// What the local run ended up using, so Program can print it once at startup.
/// AdminPassword is null when the developer configured one themselves — their secret
/// is not something to echo into the console.
public sealed record LocalDevSetup(
    string DatabaseFile,
    string AdminEmail,
    string? AdminPassword,
    bool ReplacedPostgresConnection);

// Local development runs on a SQLite file, never on Neon: `dotnet run` brings up the
// API on App_Data/dip-local.db, a seeded SuperAdmin you can actually log in as, and the
// SQLite file so a developer needs neither Postgres nor a cloud account.
//
// This deliberately overrides a Postgres ConnectionStrings:Default left in user-secrets
// — the point is that a local run never touches the shared database. Export
// Database__Provider=Postgres to opt back in for a session.
//
// Hard rule 5 still holds — nothing secret is committed. The JWT key and the admin
// password are generated on first run and kept in App_Data/dev-secrets.json, which is
// git-ignored along with the rest of App_Data.
public static class LocalDevDefaults
{
    private const string SecretsFileName = "dev-secrets.json";
    private const string DatabaseFileName = "dip-local.db";
    private const string DefaultAdminEmail = "admin@dip.local";

    /// Applies the defaults and returns what they were, or null when this run is not a
    /// plain local one: anything but Development, the `dotnet ef` design-time host
    /// (migrations are authored against Postgres), and an explicit
    /// Database:Provider=Postgres — which is also what the integration tests set.
    public static LocalDevSetup? Apply(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment()) return null;
        if (EF.IsDesignTime) return null;

        var configuredProvider = builder.Configuration[DatabaseProviderResolver.ConfigKey];
        if (!string.IsNullOrWhiteSpace(configuredProvider)
            && DatabaseProviderResolver.Resolve(builder.Configuration) == DatabaseProvider.Postgres)
        {
            return null;
        }

        var appData = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appData);

        var secrets = LoadOrCreateSecrets(Path.Combine(appData, SecretsFileName));
        var databaseFile = Path.Combine(appData, DatabaseFileName);

        // A SQLite connection string the developer set themselves is theirs to keep;
        // a Postgres one (typically Neon, in user-secrets) is replaced.
        var configured = builder.Configuration.GetConnectionString("Default");
        var keepConfigured = !string.IsNullOrWhiteSpace(configured)
            && DatabaseProviderResolver.Infer(configured) == DatabaseProvider.Sqlite;

        var values = new Dictionary<string, string?> { ["Database:Provider"] = "Sqlite" };
        if (!keepConfigured)
        {
            // Absolute, so it does not matter which directory `dotnet run` was invoked from.
            values["ConnectionStrings:Default"] = $"Data Source={databaseFile};Default Timeout=30";
        }

        var generatedPassword = string.IsNullOrWhiteSpace(builder.Configuration["Seed:AdminPassword"]);
        Fill(builder, values, "Jwt:Key", secrets.JwtKey);
        Fill(builder, values, "Seed:AdminEmail", DefaultAdminEmail);
        Fill(builder, values, "Seed:AdminPassword", secrets.AdminPassword);

        builder.Configuration.AddInMemoryCollection(values);

        return new LocalDevSetup(
            keepConfigured ? configured! : databaseFile,
            builder.Configuration["Seed:AdminEmail"] ?? DefaultAdminEmail,
            generatedPassword ? secrets.AdminPassword : null,
            !keepConfigured && !string.IsNullOrWhiteSpace(configured));
    }

    // In-memory config is the last source and therefore wins, so only defaults for keys
    // the developer has not set themselves are added.
    private static void Fill(
        WebApplicationBuilder builder, IDictionary<string, string?> values, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[key])) values[key] = value;
    }

    private sealed record DevSecrets(string JwtKey, string AdminPassword);

    private static DevSecrets LoadOrCreateSecrets(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<DevSecrets>(File.ReadAllText(path));
                if (existing is not null
                    && !string.IsNullOrWhiteSpace(existing.JwtKey)
                    && !string.IsNullOrWhiteSpace(existing.AdminPassword))
                {
                    return existing;
                }
            }
            catch (JsonException)
            {
                // Corrupt file — regenerate below rather than block the run.
            }
        }

        var secrets = new DevSecrets(
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            NewAdminPassword());

        File.WriteAllText(path, JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = true }));
        return secrets;
    }

    // Shaped to satisfy the Identity rules configured in AddInfrastructure:
    // 8+ characters with an upper case letter, a lower case letter and a digit.
    private static string NewAdminPassword()
        => "Dev" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant() + "A1";
}
