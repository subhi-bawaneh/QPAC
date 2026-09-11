using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    // /health is what an uptime monitor polls: process and database only, so a Drive
    // outage never makes the API look down.
    [Fact]
    public async Task Health_ReportsTheDatabaseOnly()
    {
        if (!_factory.IsPostgresAvailable) return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task HealthDetail_ReportsEachDependencySeparately()
    {
        if (!_factory.IsPostgresAvailable) return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/detail");

        // Drive is unconfigured in tests, which is Degraded — the platform runs fine
        // on uploaded workbooks — so the overall report is not Healthy but must answer.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var checks = body.GetProperty("checks").EnumerateArray().ToList();

        checks.Should().HaveCount(1, "Drive is gone, so the database is the only dependency");

        var database = checks.Single(c => c.GetProperty("name").GetString() == "database");
        database.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task HealthEndpoints_NeedNoAuthentication()
    {
        if (!_factory.IsPostgresAvailable) return;

        var client = _factory.CreateClient();

        (await client.GetAsync("/health")).StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/health/detail")).StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
