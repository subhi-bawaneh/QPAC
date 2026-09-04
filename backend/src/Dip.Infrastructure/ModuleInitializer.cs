using System.Runtime.CompilerServices;

namespace Dip.Infrastructure;

// Runs once when the Dip.Infrastructure assembly loads, before any Npgsql
// resolver caches its DateTime handling. PLAN.md § 1 requires naive
// timestamps that match Excel; see docs/decisions/001-datetime.md.
//
// A static constructor on DipDbContext is too late — the resolver is
// initialised the first time an NpgsqlDataSource is built, which can happen
// before any DipDbContext instance is constructed (e.g. when other services
// resolve Npgsql-related bindings during host bootstrap).
#pragma warning disable CA2255
internal static class ModuleInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }
}
#pragma warning restore CA2255
