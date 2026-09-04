using Dip.Api;
using Dip.Infrastructure;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
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
        await db.Database.MigrateAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync();
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
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health");
    app.MapControllers();

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

// Expose Program for WebApplicationFactory in integration tests.
public partial class Program;
