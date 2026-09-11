using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Dip.Api.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class AuthFlowTests
{
    private readonly DipApiFactory _factory;

    public AuthFlowTests(DipApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        if (!_factory.IsPostgresAvailable) return;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = DipApiFactory.SuperAdminEmail,
            password = "WrongPassword!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithMissingEmail_Returns400()
    {
        if (!_factory.IsPostgresAvailable) return;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "",
            password = "whatever",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokenAndPermissions()
    {
        if (!_factory.IsPostgresAvailable) return;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = DipApiFactory.SuperAdminEmail,
            password = DipApiFactory.SuperAdminPassword,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();

        var user = body.GetProperty("user");
        user.GetProperty("email").GetString().Should().Be(DipApiFactory.SuperAdminEmail);
        var permissions = user.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToArray();
        permissions.Should().Contain("users.manage");
        permissions.Should().Contain("import.run");
        permissions.Should().Contain("files.manage");
        permissions.Should().Contain("documents.edit");

        var roles = user.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray();
        roles.Should().Contain("SuperAdmin");
    }

    [Fact]
    public async Task Me_WithValidToken_Returns200WithRolesAndPermissions()
    {
        if (!_factory.IsPostgresAvailable) return;

        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, DipApiFactory.SuperAdminEmail, DipApiFactory.SuperAdminPassword);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(DipApiFactory.SuperAdminEmail);
        var permissions = body.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToArray();
        permissions.Should().Contain("reports.view");
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        if (!_factory.IsPostgresAvailable) return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewPairAndRevokesOld()
    {
        if (!_factory.IsPostgresAvailable) return;

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = DipApiFactory.SuperAdminEmail,
            password = DipApiFactory.SuperAdminPassword,
        });
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginBody.GetProperty("refreshToken").GetString()!;

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshBody = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        refreshBody.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        refreshBody.GetProperty("refreshToken").GetString().Should().NotBe(refreshToken);

        // The old refresh token is now revoked — trying to reuse it should 401.
        var reuseAttempt = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        reuseAttempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }
}
