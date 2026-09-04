using Dip.Application.Abstractions;
using Dip.Infrastructure;
using Dip.Infrastructure.Excel;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dip.Infrastructure.Tests;

// Reuses PostgresFixture (per-test-class isolated schema) + wires the
// importer DI graph. On InitializeAsync it migrates + seeds so the QPAC
// project + status mappings exist before the importers run.
public sealed class ImporterFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();
    private ServiceProvider? _provider;

    public bool IsAvailable => _postgres.IsAvailable;
    public string Schema => _postgres.Schema;

    public Guid QpacProjectId { get; private set; } = Guid.Empty;

    public IServiceScope CreateScope() => _provider!.CreateScope();

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        if (!IsAvailable) return;

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.ConnectionString,
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
        QpacProjectId = await db.Projects.Where(p => p.Code == SeedData.QpacProjectCode)
            .Select(p => p.Id)
            .FirstAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null) await _provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
