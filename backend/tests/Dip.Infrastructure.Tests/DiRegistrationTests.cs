using Dip.Application.Abstractions;
using Dip.Infrastructure;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Infrastructure.Tests;

public class DiRegistrationTests
{
    [Fact]
    public void AddInfrastructure_ThrowsWhenConnectionStringMissing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder().Build();

        var act = () => services.AddInfrastructure(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionStrings:Default*");
    }

    [Fact]
    public void AddInfrastructure_RegistersDbContextAndDipDbContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=dip;Username=x;Password=x",
            })
            .Build();

        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        provider.GetService<DipDbContext>().Should().NotBeNull();
        provider.GetService<IDipDbContext>().Should().NotBeNull();
        provider.GetService<IDipDbContext>().Should().BeSameAs(provider.GetService<DipDbContext>());
    }
}
