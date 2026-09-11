using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.Api.IntegrationTests;

// Shared plumbing for the flow tests.
internal static class TestHelpers
{
    public static readonly Guid QpacProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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

    // Seeds a TidpFile and its documents straight into the database. Stage 2 has no
    // upload endpoint — stage 3 adds it — so a test that needs a populated register
    // writes one rather than going through a route that does not exist yet.
    public static async Task<(Guid TidpFileId, IReadOnlyList<Guid> DocumentIds)> SeedDocumentsAsync(
        DipApiFactory factory, Guid projectId, int count = 3, string discipline = "Structural")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();

        var disciplineRow = await db.Disciplines
            .FirstOrDefaultAsync(d => d.ProjectId == projectId && d.CorporateName == discipline);
        if (disciplineRow is null)
        {
            disciplineRow = new Discipline
            {
                ProjectId = projectId,
                Code = discipline[..Math.Min(3, discipline.Length)].ToUpperInvariant(),
                CorporateName = discipline,
            };
            db.Disciplines.Add(disciplineRow);
        }

        var now = DateTime.UtcNow;
        var file = new TidpFile
        {
            ProjectId = projectId,
            DisciplineId = disciplineRow.Id,
            DocumentReference = "SEED",
            FileName = $"seed-{Guid.NewGuid():N}.xlsx",
            UploadedBy = "test",
            UploadedAt = now,
            Status = TidpFileStatus.Imported,
            CreatedAt = now,
            CreatedBy = "test",
            UpdatedAt = now,
            UpdatedBy = "test",
        };
        db.TidpFiles.Add(file);

        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var document = new Document
            {
                ProjectId = projectId,
                TidpFileId = file.Id,
                DisciplineId = disciplineRow.Id,
                DocumentNumber = $"QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ{i:0000}",
                Title = $"Seed document {i}",
                F01Project = "QF01012",
                F02Originator = "NES",
                F03Contract = "C04518",
                F04DocType = "SDW",
                F05Discipline = "STL",
                F06Zone = "00",
                F07Building = "Z00000",
                F08ADrawingType = "0",
                F08BLevel = "ZZ",
                F08CSequence = $"{i:0000}",
                CorporateDiscipline = discipline,
                CreatedAt = now,
                CreatedBy = "test",
                UpdatedAt = now,
                UpdatedBy = "test",
            };
            db.Documents.Add(document);
            ids.Add(document.Id);
        }

        await db.SaveChangesAsync();
        return (file.Id, ids);
    }

    // Imports a sample TIDP workbook straight through the importer. Stage 2 has no
    // upload route; stage 3 adds one and the flow tests move onto it.
    public static async Task<(Guid TidpFileId, Dip.Infrastructure.Importers.ImportResult Result)> ImportTidpAsync(
        DipApiFactory factory, Guid projectId, string fileName)
    {
        Guid fileId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            var disciplineId = await db.Disciplines
                .Where(d => d.ProjectId == projectId)
                .Select(d => d.Id)
                .FirstAsync();

            var now = DateTime.UtcNow;
            var file = new TidpFile
            {
                ProjectId = projectId,
                DisciplineId = disciplineId,
                FileName = fileName,
                UploadedBy = "test",
                UploadedAt = now,
                Status = TidpFileStatus.Importing,
                CreatedAt = now,
                CreatedBy = "test",
                UpdatedAt = now,
                UpdatedBy = "test",
            };
            db.TidpFiles.Add(file);
            await db.SaveChangesAsync();
            fileId = file.Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var importer = scope.ServiceProvider
                .GetRequiredService<Dip.Infrastructure.Importers.TidpImporter>();
            await using var stream = File.OpenRead(SamplePath(fileName));
            var result = await importer.ImportAsync(projectId, stream, fileId, "test", CancellationToken.None);

            var db = scope.ServiceProvider.GetRequiredService<DipDbContext>();
            await db.TidpFiles.Where(t => t.Id == fileId)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.Status, TidpFileStatus.Imported));

            return (fileId, result);
        }
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
