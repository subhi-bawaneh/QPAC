using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Api.IntegrationTests;

// The editable Lists page's backend (refactor-plan § 5.8 and § 3 R9).
[Collection(IntegrationTestCollection.Name)]
public class PicklistCrudTests
{
    private readonly DipApiFactory _factory;

    public PicklistCrudTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task EveryListAppears_EvenWhenEmpty()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Picklist groups");

        var groups = await TestHelpers.GetJsonAsync(admin, $"/api/projects/{projectId}/picklists");
        groups.GetArrayLength().Should().Be(Enum.GetValues<PicklistField>().Length,
            "the page renders one tab per list");
        groups.EnumerateArray().Select(g => g.GetProperty("field").GetString())
            .Should().Contain(["Author", "Classification", "CorporateDiscipline"]);
    }

    [Fact]
    public async Task Create_Update_Delete_Restore_RoundTrip()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Picklist crud");

        // Create.
        var created = await CreateAsync(admin, projectId, "Author", "ACME", "Acme Contracting");
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = item.GetProperty("id").GetGuid();
        item.GetProperty("sortOrder").GetInt32().Should().Be(1);
        item.GetProperty("isDeleted").GetBoolean().Should().BeFalse();

        // Creating the same code again is a 409.
        (await CreateAsync(admin, projectId, "Author", "ACME", "again"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Update.
        var updated = await admin.PutAsJsonAsync($"/api/picklists/{id}", new
        {
            code = "ACME-2",
            description = "Acme Contracting Ltd",
            sortOrder = 1,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updated.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString().Should().Be("ACME-2");

        // A code another live row holds is a 409.
        await CreateAsync(admin, projectId, "Author", "OTHER", "Other");
        var collide = await admin.PutAsJsonAsync($"/api/picklists/{id}", new
        {
            code = "OTHER",
            description = "x",
            sortOrder = 1,
        });
        collide.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Soft delete hides the row.
        (await admin.DeleteAsync($"/api/picklists/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AuthorCodesAsync(admin, projectId, includeDeleted: false)).Should().NotContain("ACME-2");

        var withDeleted = await AuthorsAsync(admin, projectId, includeDeleted: true);
        withDeleted.Single(a => a.GetProperty("id").GetGuid() == id)
            .GetProperty("isDeleted").GetBoolean().Should().BeTrue();

        // Restore.
        var restored = await admin.PostAsync($"/api/picklists/{id}/restore", null);
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
        (await AuthorCodesAsync(admin, projectId, includeDeleted: false)).Should().Contain("ACME-2");
    }

    [Fact]
    public async Task CreatingADeletedCode_RestoresTheSameRow()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Picklist restore-on-create");

        var created = await CreateAsync(admin, projectId, "Scale", "1:7", "");
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await admin.DeleteAsync($"/api/picklists/{id}")).EnsureSuccessStatusCode();

        var again = await CreateAsync(admin, projectId, "Scale", "1:7", "recreated");
        again.StatusCode.Should().Be(HttpStatusCode.OK, "the deleted row is restored, not duplicated");
        var body = await again.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("restored").GetBoolean().Should().BeTrue();
        body.GetProperty("item").GetProperty("id").GetGuid().Should().Be(id);
        body.GetProperty("item").GetProperty("description").GetString().Should().Be("recreated");
    }

    [Fact]
    public async Task Reorder_SetsSortOrderOneToN()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Picklist reorder");

        var ids = new List<Guid>();
        foreach (var code in new[] { "A", "B", "C" })
        {
            var created = await CreateAsync(admin, projectId, "Scale", code, "");
            ids.Add((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }

        var reversed = Enumerable.Reverse(ids).ToList();
        var response = await admin.PutAsJsonAsync(
            $"/api/projects/{projectId}/picklists/Scale/order", new { ids = reversed });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var groups = await TestHelpers.GetJsonAsync(admin, $"/api/projects/{projectId}/picklists");
        var scale = groups.EnumerateArray().Single(g => g.GetProperty("field").GetString() == "Scale");
        scale.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("code").GetString())
            .Should().Equal("C", "B", "A");
        scale.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sortOrder").GetInt32())
            .Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task StatusMappings_SupportCrudAndSoftDelete()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Status mapping crud");

        var seeded = await TestHelpers.GetJsonAsync(admin, $"/api/projects/{projectId}/status-mappings");
        seeded.GetArrayLength().Should().Be(22, "SeedData ships 22 mappings");

        var created = await admin.PostAsJsonAsync($"/api/projects/{projectId}/status-mappings", new
        {
            aconexStatus = "Awaiting Countersign",
            status = "UnderReview",
            isLegacy = false,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var updated = await admin.PutAsJsonAsync($"/api/status-mappings/{id}", new
        {
            aconexStatus = "Awaiting Countersign",
            status = "Approved",
            isLegacy = true,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updated.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString().Should().Be("Approved");

        (await admin.DeleteAsync($"/api/status-mappings/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await TestHelpers.GetJsonAsync(admin, $"/api/projects/{projectId}/status-mappings"))
            .GetArrayLength().Should().Be(22);
        (await TestHelpers.GetJsonAsync(
                admin, $"/api/projects/{projectId}/status-mappings?includeDeleted=true"))
            .GetArrayLength().Should().Be(23);

        (await admin.PostAsync($"/api/status-mappings/{id}/restore", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await TestHelpers.GetJsonAsync(admin, $"/api/projects/{projectId}/status-mappings"))
            .GetArrayLength().Should().Be(23);
    }

    [Fact]
    public async Task ReImportOfThePicklistsWorkbook_SkipsDeletedCodes()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Picklist re-import");
        var folderId = await TestHelpers.CreateFolderAsync(
            admin, $"PicklistReimport-{Guid.NewGuid():N}", projectId: projectId);

        await TestHelpers.ImportedAsync(admin, folderId, "PickLists.xlsx");

        var authors = await AuthorsAsync(admin, projectId, includeDeleted: false);
        var tke = authors.Single(a => a.GetProperty("code").GetString() == "TKE");
        var tkeId = tke.GetProperty("id").GetGuid();
        (await admin.DeleteAsync($"/api/picklists/{tkeId}")).EnsureSuccessStatusCode();

        // Uploading the same workbook again re-imports it; the deleted code stays gone.
        var response = await TestHelpers.UploadAsync(admin, folderId, "PickLists.xlsx");
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var fileId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fileId").GetGuid();
        await TestHelpers.WaitForImportAsync(admin, folderId, fileId);

        (await AuthorCodesAsync(admin, projectId, includeDeleted: false))
            .Should().NotContain("TKE", "a re-import must not resurrect a deleted code");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var batch = await db.ImportBatches.AsNoTracking()
            .Where(b => b.FolderFileId == fileId && b.Completed)
            .OrderByDescending(b => b.ImportedAt)
            .FirstAsync();
        batch.RowsSkipped.Should().Be(1);
        batch.Log.Should().Contain("TKE");
    }

    [Fact]
    public async Task Viewer_CannotWriteLists()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var viewer = await TestHelpers.AuthedAsync(_factory, admin, "Viewer", "lists-viewer");

        var response = await CreateAsync(viewer, TestHelpers.QpacProjectId, "Author", "NOPE", "");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ helpers

    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient client, Guid projectId, string field, string code, string description) =>
        client.PostAsJsonAsync($"/api/projects/{projectId}/picklists",
            new { field, code, description, sortOrder = (int?)null });

    private static async Task<List<JsonElement>> AuthorsAsync(
        HttpClient admin, Guid projectId, bool includeDeleted)
    {
        var groups = await TestHelpers.GetJsonAsync(
            admin, $"/api/projects/{projectId}/picklists?includeDeleted={includeDeleted}");
        return groups.EnumerateArray()
            .Single(g => g.GetProperty("field").GetString() == "Author")
            .GetProperty("items").EnumerateArray().ToList();
    }

    private static async Task<List<string?>> AuthorCodesAsync(
        HttpClient admin, Guid projectId, bool includeDeleted) =>
        (await AuthorsAsync(admin, projectId, includeDeleted))
        .Select(a => a.GetProperty("code").GetString())
        .ToList();
}
