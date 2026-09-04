using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Dip.Api.IntegrationTests;

public class HealthCheckTests : IClassFixture<DipWebApplicationFactory>
{
    private readonly DipWebApplicationFactory _factory;

    public HealthCheckTests(DipWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("Healthy");
    }
}

// Injects a placeholder connection string so the DbContext registers cleanly even
// when no real Postgres is available. Health check does not touch the DB.
public sealed class DipWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Database=dip_test;Username=x;Password=x",
            ["Jwt:Key"] = "test-key-not-secret-32chars-minimum!!",
        }));
        return base.CreateHost(builder);
    }
}
