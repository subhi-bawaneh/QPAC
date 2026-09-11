using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence;

// The entity configurations are written for Postgres — that is the production engine
// and the one PLAN.md § 1 specifies. Rather than sprinkle `if (sqlite)` through every
// IEntityTypeConfiguration, the Postgres-only bits are normalised away here in one
// pass when the context happens to be running on SQLite for local development.
//
// Nothing in this file runs on Postgres: EF caches a model per provider, so the
// production model is byte-for-byte what the configurations declare.
internal static class SqliteModelTweaks
{
    // Store types SQLite has never heard of. Clearing them lets the SQLite provider
    // pick its own affinity (TEXT for timestamps and json, BLOB for bytea).
    private static readonly HashSet<string> PostgresOnlyColumnTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "timestamp without time zone",
        "timestamp with time zone",
        "jsonb",
        "json",
        "bytea",
    };

    public static void Apply(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetDeclaredProperties())
            {
                var columnType = property.GetColumnType();
                if (columnType is not null && PostgresOnlyColumnTypes.Contains(columnType))
                {
                    property.SetColumnType(null);
                }

                // SQLite has no decimal type: EF stores decimals as TEXT, which orders
                // and aggregates lexicographically ("10" < "9"). Doubles are exact enough
                // for the 4-decimal weights we keep and behave correctly in SQL.
                var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (clrType == typeof(decimal))
                {
                    property.SetProviderClrType(typeof(double));
                    property.SetPrecision(null);
                    property.SetScale(null);
                }
            }
        }
    }
}
