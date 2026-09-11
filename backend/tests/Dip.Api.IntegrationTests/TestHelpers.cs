using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.Api.IntegrationTests;

// Shared plumbing for the flow tests. Imports are automatic now (decision D5), so
// a test uploads a workbook and then waits for the ImportWorker to finish with it.
internal static class TestHelpers
{
    public static readonly Guid QpacProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static async Task<HttpClient> AuthedAdminAsync(DipApiFactory factory)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = DipApiFactory.SuperAdminEmail,
            password = DipApiFactory.SuperAdminPassword,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    public static async Task<HttpClient> AuthedAsync(
        DipApiFactory factory, HttpClient admin, string role, string label)
    {
        var email = $"{label}-{Guid.NewGuid():N}@dip.test";
        const string password = "IntegrationTest!23";
        (await admin.PostAsJsonAsync("/api/users", new
        {
            email,
            password,
            fullName = label,
            roles = new[] { role },
        })).EnsureSuccessStatusCode();

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("accessToken").GetString());
        return client;
    }

    // A scratch project for tests that write Live document rows: the seeded QPAC
    // project is shared by the whole collection, and one test's Live import would
    // otherwise show up as another test's promote conflict.
    public static async Task<Guid> NewProjectAsync(DipApiFactory factory, string label)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.DipDbContext>();
        var project = new Domain.Entities.Project
        {
            Code = $"T{Guid.NewGuid():N}"[..12],
            Name = label,
        };
        db.Projects.Add(project);

        // Disciplines and status mappings are per project and every importer reads them.
        foreach (var discipline in Infrastructure.Seeding.SeedData.Disciplines)
        {
            db.Disciplines.Add(new Domain.Entities.Discipline
            {
                ProjectId = project.Id,
                Code = discipline.Code,
                CorporateName = discipline.CorporateName,
            });
        }


        foreach (var mapping in Infrastructure.Seeding.SeedData.StatusMappings)
        {
            db.StatusMappings.Add(new Domain.Entities.StatusMapping
            {
                ProjectId = project.Id,
                AconexStatus = mapping.AconexStatus,
                Status = mapping.Status,
                IsLegacy = mapping.IsLegacy,
            });
        }

        // Sequence widths are per project too, and the parser reads them for every row.
        foreach (var (docType, width) in Infrastructure.Seeding.SeedPicklists.SerialWidths)
        {
            db.DocumentTypeSerials.Add(new Domain.Entities.DocumentTypeSerial
            {
                ProjectId = project.Id,
                DocType = docType,
                SequenceWidth = width,
            });
        }

        await db.SaveChangesAsync();
        return project.Id;
    }

    public static async Task<Guid> CreateFolderAsync(
        HttpClient admin, string name, Guid? parentId = null, Guid? projectId = null)
    {
        var response = await admin.PostAsJsonAsync("/api/folders", new
        {
            projectId = projectId ?? QpacProjectId,
            parentId,
            name,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "create folder failed: {0}", await response.Content.ReadAsStringAsync());
        return Guid.Parse((await response.Content.ReadAsStringAsync()).Trim('"'));
    }

    public static async Task<JsonElement> SetTargetAsync(HttpClient admin, Guid folderId, string target)
    {
        var response = await admin.PutAsJsonAsync($"/api/folders/{folderId}/target", new { target });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "set target failed: {0}", await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, Guid folderId, string fileName, byte[]? bytes = null)
    {
        var multipart = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes ?? await File.ReadAllBytesAsync(SamplePath(fileName)));
        content.Headers.ContentType = new MediaTypeHeaderValue(XlsxContentType);
        multipart.Add(content, "file", fileName);
        return await client.PostAsync($"/api/folders/{folderId}/files", multipart);
    }

    public static async Task<Guid> UploadSampleAsync(HttpClient admin, Guid folderId, string fileName)
    {
        var response = await UploadAsync(admin, folderId, fileName);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "upload failed: {0}", await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("fileId").GetGuid();
    }

    // The worker imports on its own thread; the flow tests poll the folder until it
    // reports a terminal state for the file.
    public static async Task<JsonElement> WaitForImportAsync(
        HttpClient client, Guid folderId, Guid fileId, int timeoutSeconds = 120)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        JsonElement file = default;
        var found = false;

        while (DateTime.UtcNow < deadline)
        {
            var detail = await GetJsonAsync(client, $"/api/folders/{folderId}");
            foreach (var candidate in detail.GetProperty("files").EnumerateArray())
            {
                if (candidate.GetProperty("id").GetGuid() != fileId) continue;
                file = candidate;
                found = true;
                var state = candidate.GetProperty("state").GetString();
                if (state is "Imported" or "Failed") return candidate;
            }

            await Task.Delay(250);
        }

        found.Should().BeTrue("file {0} should appear under folder {1}", fileId, folderId);
        throw new TimeoutException(
            $"File {fileId} was still {file.GetProperty("state").GetString()} after {timeoutSeconds}s");
    }

    public static async Task<JsonElement> ImportedAsync(
        HttpClient admin, Guid folderId, string fileName)
    {
        var fileId = await UploadSampleAsync(admin, folderId, fileName);
        var file = await WaitForImportAsync(admin, folderId, fileId);
        file.GetProperty("state").GetString().Should().Be("Imported",
            "import error: {0}", file.GetProperty("importError").GetString());
        return file;
    }

    public static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "GET {0} should succeed", url);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static string SamplePath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", fileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Cannot locate samples/{fileName}");
    }
}
