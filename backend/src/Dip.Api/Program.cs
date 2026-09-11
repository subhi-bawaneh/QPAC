using Dip.Api;
using Dip.Infrastructure;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.SignalR;
using Serilog;

// Bootstrap logger — set once per process. WebApplicationFactory reboots the
// host per test class and Serilog's static logger throws "already frozen" on
// a second CreateBootstrapLogger call, so we silently ignore that path.
try
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateBootstrapLogger();
}
catch (InvalidOperationException)
{
    // Logger already initialized in a prior test host — reuse it.
}

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Unconfigured local run? Fall back to a SQLite file, generated dev credentials and
    // the checked-out Drive mirror. Returns null for every configured environment.
    var localDev = LocalDevDefaults.Apply(builder);

    builder.Host.UseSerilog((ctx, sp, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(sp)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApi(builder.Configuration);

    var app = builder.Build();

    // Auto-migrate + seed at startup unless explicitly disabled.
    // Tests set `Startup:SkipMigration=true` to control their own lifecycle.
    if (!app.Configuration.GetValue("Startup:SkipMigration", false))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        await InitializeDatabaseAsync(db, DatabaseProviderResolver.Resolve(app.Configuration));
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync();
    }

    if (localDev is not null)
    {
        Log.Information("Local development mode — SQLite at {Database}", localDev.DatabaseFile);
        Log.Information(
            localDev.AdminPassword is null
                ? "Sign in as {Email} with your configured Seed:AdminPassword"
                : "Sign in as {Email} / {Password} (kept in App_Data/dev-secrets.json)",
            localDev.AdminEmail, localDev.AdminPassword);

        if (localDev.ReplacedPostgresConnection)
        {
            Log.Warning(
                "Ignoring the configured Postgres connection string — a local run never "
                + "touches the shared database. Export Database__Provider=Postgres to use it.");
        }
    }

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // Liveness for the uptime monitor: process + database, nothing external.
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("core"),
    });

    // Everything, including Drive reachability, as JSON for an operator.
    app.MapHealthChecks("/health/detail", new HealthCheckOptions
    {
        ResponseWriter = WriteHealthReportAsync,
    });
    app.MapControllers();
    app.MapHub<Dip.Api.Hubs.SyncHub>("/hubs/sync");

    Log.Information("DIP API starting");
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "DIP API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Postgres owns a migration history and is migrated. SQLite is a throwaway local file
// with no migrations of its own (the Init migration is Npgsql SQL), so its schema comes
// straight from the model — change the model and delete the file, see
// backend/scripts/reset-local-db.sh.
static async Task InitializeDatabaseAsync(DipDbContext db, DatabaseProvider provider)
{
    if (provider != DatabaseProvider.Sqlite)
    {
        await db.Database.MigrateAsync();
        return;
    }

    var dataSource = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
    var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

    await db.Database.EnsureCreatedAsync();

    // The import and sync workers write while the API serves requests; without WAL the
    // default rollback journal locks readers out and they fail with SQLITE_BUSY.
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");
}

// Per-check JSON so an operator can see which dependency is unhappy and why.
static Task WriteHealthReportAsync(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
{
    context.Response.ContentType = "application/json";

    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            durationMs = entry.Value.Duration.TotalMilliseconds,
            tags = entry.Value.Tags,
        }),
    });
}

// Expose Program for WebApplicationFactory in integration tests.
public partial class Program;
