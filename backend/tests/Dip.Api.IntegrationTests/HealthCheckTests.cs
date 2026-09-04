using FluentAssertions;
using Xunit;

namespace Dip.Api.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class HealthCheckTests
{
    private readonly DipApiFactory _factory;

    public HealthCheckTests(DipApiFactory factory)
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
