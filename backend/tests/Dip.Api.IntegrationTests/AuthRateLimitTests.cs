using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Login and refresh are the only unauthenticated surface, so they are the only
// endpoints rate limited (PLAN.md § 7.3).
//
// The shared factory keeps the production-ish default of 20 per minute, which the
// rest of the suite would otherwise trip while logging in. This class boots its own
// host with a limit of 3 against the same schema, so the 429 is proven without
// making every other test fragile.
[Collection(IntegrationTestCollection.Name)]
public class AuthRateLimitTests
{
    private const int PermitLimit = 3;

    private readonly DipApiFactory _shared;

    public AuthRateLimitTests(DipApiFactory shared) => _shared = shared;

    private sealed class RateLimitedFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public RateLimitedFactory(string connectionString) => _connectionString = connectionString;

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // See DipApiFactory: pin the provider so the local SQLite defaults
                    // never apply to a test host.
                    ["Database:Provider"] = "Postgres",
                    ["ConnectionStrings:Default"] = _connectionString,
                    ["Jwt:Key"] = "integration-test-key-not-secret-32bytes-min!",
                    ["RateLimit:AuthPermitPerWindow"] = PermitLimit.ToString(),
                    ["RateLimit:AuthWindowSeconds"] = "60",
                    // The shared fixture owns the schema; this host must not migrate it.
                    ["Startup:SkipMigration"] = "true",
                }));

            return base.CreateHost(builder);
        }
    }

    [Fact]
    public async Task RepeatedLoginAttempts_AreRejectedWith429()
    {
        if (!_shared.IsPostgresAvailable) return;

        using var factory = new RateLimitedFactory(_shared.ConnectionString);
        var client = factory.CreateClient();

        // Wrong password on purpose: the limiter must not depend on the outcome.
        var attempt = new { email = "nobody@dip.test", password = "WrongPassw0rd!" };

        for (var i = 0; i < PermitLimit; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/login", attempt);
            allowed.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "attempt {0} is within the window and should be answered normally", i + 1);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/login", attempt);
        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task TheLimitCoversRefreshToo_ButNotTheRestOfTheApi()
    {
        if (!_shared.IsPostgresAvailable) return;

        using var factory = new RateLimitedFactory(_shared.ConnectionString);
        var client = factory.CreateClient();

        for (var i = 0; i < PermitLimit; i++)
        {
            await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "not-a-token" });
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "not-a-token" });
        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Everything else needs a token already, so it carries no limiter: an
        // exhausted auth window must not lock a signed-in user out of the API.
        var elsewhere = await client.GetAsync("/health");
        elsewhere.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
