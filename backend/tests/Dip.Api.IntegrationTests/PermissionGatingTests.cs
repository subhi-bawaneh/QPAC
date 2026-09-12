using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Dip.Api.IntegrationTests;

// Verifies role/permission gating end-to-end via the authorization pipeline behavior.
// - SuperAdmin has users.manage → can list/create.
// - A freshly-created Viewer user does NOT have users.manage → 403 on the same endpoints.
[Collection(IntegrationTestCollection.Name)]
public class PermissionGatingTests
{
    private readonly DipApiFactory _factory;

    public PermissionGatingTests(DipApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Viewer_CannotListUsers()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var client = _factory.CreateClient();
        var adminToken = await LoginAsAsync(client, DipApiFactory.SuperAdminEmail, DipApiFactory.SuperAdminPassword);

        // Create a viewer user under a unique email so tests don't collide across runs.
        var viewerEmail = $"viewer-{Guid.NewGuid():N}@dip.test";
        var viewerPassword = "ViewerPassw0rd!";
        await CreateUserAsAdminAsync(client, adminToken, viewerEmail, viewerPassword, "Viewer Test", roles: new[] { "Viewer" });

        var viewerToken = await LoginAsAsync(client, viewerEmail, viewerPassword);

        var listClient = _factory.CreateClient();
        listClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);
        var response = await listClient.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Editor_CannotCreateUsers()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var client = _factory.CreateClient();
        var adminToken = await LoginAsAsync(client, DipApiFactory.SuperAdminEmail, DipApiFactory.SuperAdminPassword);

        var editorEmail = $"editor-{Guid.NewGuid():N}@dip.test";
        var editorPassword = "EditorPassw0rd!";
        await CreateUserAsAdminAsync(client, adminToken, editorEmail, editorPassword, "Editor Test", roles: new[] { "Editor" });

        var editorToken = await LoginAsAsync(client, editorEmail, editorPassword);

        var createClient = _factory.CreateClient();
        createClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", editorToken);
        var response = await createClient.PostAsJsonAsync("/api/users", new
        {
            email = "wontbecreated@dip.test",
            password = "SomePassw0rd!",
            fullName = "Nope",
            roles = new[] { "Viewer" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SuperAdmin_CanCreateAssignRoleAndSetDisciplines()
    {
        if (!_factory.IsSqlServerAvailable) return;

        var client = _factory.CreateClient();
        var adminToken = await LoginAsAsync(client, DipApiFactory.SuperAdminEmail, DipApiFactory.SuperAdminPassword);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"user-{Guid.NewGuid():N}@dip.test";
        var createResponse = await client.PostAsJsonAsync("/api/users", new
        {
            email,
            password = "InitialPassw0rd!",
            fullName = "New Editor",
            roles = new[] { "Editor" },
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Read the created user id from the body.
        var body = await createResponse.Content.ReadAsStringAsync();
        var userId = Guid.Parse(body.Trim('"'));

        // Promote to Manager.
        var assign = await client.PutAsJsonAsync($"/api/users/{userId}/roles", new
        {
            roles = new[] { "Manager" },
        });
        assign.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Restrict to STL discipline (STL is a valid seeded code alongside STR).
        var setDisc = await client.PutAsJsonAsync($"/api/users/{userId}/disciplines", new
        {
            disciplineCodes = new[] { "STL" },
        });
        setDisc.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify via GET /api/users/{id}.
        var detail = await client.GetAsync($"/api/users/{userId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailBody = await detail.Content.ReadFromJsonAsync<JsonElement>();
        var roles = detailBody.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray();
        roles.Should().BeEquivalentTo(new[] { "Manager" });
        var disciplines = detailBody.GetProperty("disciplineCodes").EnumerateArray().Select(d => d.GetString()).ToArray();
        disciplines.Should().BeEquivalentTo(new[] { "STL" });
    }

    private static async Task<string> LoginAsAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private static async Task CreateUserAsAdminAsync(
        HttpClient client, string adminToken, string email, string password, string fullName, string[] roles)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email, password, fullName, roles }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
