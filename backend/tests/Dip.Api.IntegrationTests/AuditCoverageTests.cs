using System.Net.Http.Json;
using System.Text.Json;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Api.IntegrationTests;

// The refactor plan deleted rollback and named the audit log as its replacement. That is
// only true if every handler that mutates something writes to it — and if something can
// read it back. Both halves are asserted here.
[Collection(IntegrationTestCollection.Name)]
public class AuditCoverageTests
{
    private readonly DipApiFactory _factory;

    public AuditCoverageTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task EditingADocument_WritesOneRowPerChangedField_AndMarksTheRowEdited()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Audit document test");
        var (_, documentIds) = await TestHelpers.SeedDocumentsAsync(_factory, projectId, count: 1);
        var documentId = documentIds[0];

        await ClearAuditAsync(projectId);

        var response = await admin.PutAsJsonAsync(
            $"/api/documents/{documentId}",
            await EditPayloadAsync(documentId, title: "A new title", scopeArea: "Design Management"));
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var rows = await db.AuditLogs.AsNoTracking()
            .Where(a => a.ProjectId == projectId && a.EntityId == documentId)
            .ToListAsync();

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(a => a.EntityName == nameof(Document));
        rows.Should().Contain(a => a.Field == "Title" && a.NewValue == "A new title");

        var document = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == documentId);
        document.IsEdited.Should().BeTrue("a person changed this row, not an import");
        document.EditedBy.Should().NotBeNullOrEmpty();
        document.EditedAt.Should().NotBeNull();
    }

    // Every list mutation: create, update, delete, restore and reorder. The audit was
    // written by two handlers before this stage and read by none, which is how a
    // drawing could disappear with nothing able to say why.
    [Fact]
    public async Task EveryPicklistMutation_WritesAnAuditRow()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Audit picklist test");
        await ClearAuditAsync(projectId);

        var created = await admin.PostAsJsonAsync($"/api/projects/{projectId}/picklists", new
        {
            field = "Building",
            code = "AUD001",
            description = "Audit test building",
            sortOrder = (int?)null,
        });
        created.EnsureSuccessStatusCode();
        var itemId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        (await admin.PutAsJsonAsync($"/api/picklists/{itemId}", new
        {
            code = "AUD002",
            description = "Renamed",
            sortOrder = 1,
        })).EnsureSuccessStatusCode();

        (await admin.DeleteAsync($"/api/picklists/{itemId}")).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/picklists/{itemId}/restore", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var actions = await db.AuditLogs.AsNoTracking()
            .Where(a => a.ProjectId == projectId && a.EntityId == itemId)
            .Select(a => a.Action)
            .ToListAsync();

        actions.Should().Contain("Create");
        actions.Should().Contain("Update");
        actions.Should().Contain("Delete");
        actions.Should().Contain("Restore");

        var item = await db.PicklistItems.AsNoTracking().SingleAsync(p => p.Id == itemId);
        item.IsEdited.Should().BeTrue();
        item.EditedBy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task EveryStatusMappingMutation_WritesAnAuditRow()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Audit mapping test");
        await ClearAuditAsync(projectId);

        var created = await admin.PostAsJsonAsync($"/api/projects/{projectId}/status-mappings", new
        {
            aconexStatus = "Z - Audit test",
            status = "UnderReview",
            isLegacy = false,
        });
        created.EnsureSuccessStatusCode();
        var mappingId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        (await admin.PutAsJsonAsync($"/api/status-mappings/{mappingId}", new
        {
            aconexStatus = "Z - Audit test",
            status = "Approved",
            isLegacy = false,
        })).EnsureSuccessStatusCode();

        (await admin.DeleteAsync($"/api/status-mappings/{mappingId}")).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var rows = await db.AuditLogs.AsNoTracking()
            .Where(a => a.ProjectId == projectId && a.EntityId == mappingId)
            .ToListAsync();

        rows.Select(r => r.Action).Should().Contain(new[] { "Create", "Update", "Delete" });
        rows.Should().Contain(a => a.Field == "Status" && a.NewValue == "Approved");
    }

    // The reader. Without it the log is written by everything and visible to nobody,
    // which is where this stage started.
    [Fact]
    public async Task TheAuditEndpoint_ReturnsAnEntitysHistoryNewestFirst()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Audit reader test");
        var (_, documentIds) = await TestHelpers.SeedDocumentsAsync(_factory, projectId, count: 1);
        var documentId = documentIds[0];
        await ClearAuditAsync(projectId);

        (await admin.PutAsJsonAsync($"/api/documents/{documentId}",
            await EditPayloadAsync(documentId, title: "First edit"))).EnsureSuccessStatusCode();

        (await admin.PutAsJsonAsync($"/api/documents/{documentId}",
            await EditPayloadAsync(documentId, title: "Second edit"))).EnsureSuccessStatusCode();

        var history = await TestHelpers.GetJsonAsync(
            admin, $"/api/projects/{projectId}/audit?entity=Document&entityId={documentId}");

        var items = history.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCountGreaterThan(1);
        items[0].GetProperty("newValue").GetString().Should().Be("Second edit",
            "the newest change is the one a history panel shows first");
        items.Should().OnlyContain(i => i.GetProperty("entityName").GetString() == "Document");
        items.Should().OnlyContain(i => i.GetProperty("userId").GetString() != "");
    }

    // ------------------------------------------------------------------ helpers

    private async Task ClearAuditAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        await db.AuditLogs.Where(a => a.ProjectId == projectId).ExecuteDeleteAsync();
    }

    // The document editor replaces the whole row, so an edit re-sends every field. There
    // is no GET for a single document, so the current values come from the entity.
    private async Task<object> EditPayloadAsync(
        Guid documentId, string title, string? scopeArea = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
        var d = await db.Documents.AsNoTracking().SingleAsync(x => x.Id == documentId);

        return new
        {
            title,
            extractedFromModel = d.ExtractedFromModel,
            scopeArea = scopeArea ?? d.ScopeArea,
            authoringSoftware = d.AuthoringSoftware,
            exchangeFormat = d.ExchangeFormat,
            scale = d.Scale,
            deliveryMilestone = d.DeliveryMilestone,
            packageName = d.PackageName,
            activityId = d.ActivityId,
            classificationCode = d.ClassificationCode,
            f01Project = d.F01Project,
            f02Originator = d.F02Originator,
            f03Contract = d.F03Contract,
            f04DocType = d.F04DocType,
            f05Discipline = d.F05Discipline,
            f06Zone = d.F06Zone,
            f07Building = d.F07Building,
            f08ADrawingType = d.F08ADrawingType,
            f08BLevel = d.F08BLevel,
            f08CSequence = d.F08CSequence,
            corporateDiscipline = d.CorporateDiscipline,
        };
    }
}
