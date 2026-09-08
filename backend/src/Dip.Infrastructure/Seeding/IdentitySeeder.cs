using System.Security.Claims;
using Dip.Application.Authorization;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Identity;
using Dip.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dip.Infrastructure.Seeding;

// Idempotent bootstrap: creates roles + permission claims, the QPAC Project, 9 disciplines,
// 22 status mappings, and one SuperAdmin user (email/password from env / user-secrets).
// Runs once at startup; safe to call on every boot.
public sealed class IdentitySeeder
{
    private readonly DipDbContext _db;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        DipDbContext db,
        RoleManager<ApplicationRole> roles,
        UserManager<ApplicationUser> users,
        IConfiguration configuration,
        ILogger<IdentitySeeder> logger)
    {
        _db = db;
        _roles = roles;
        _users = users;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync();
        var project = await SeedProjectAsync(ct);
        await SeedDisciplinesAsync(project.Id, ct);
        await SeedStatusMappingsAsync(project.Id, ct);
        await SeedPicklistsAsync(project.Id, ct);
        await SeedSuperAdminAsync();

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seed complete for project {Code}", project.Code);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in RoleDefinitions.AllRoles)
        {
            var existing = await _roles.FindByNameAsync(role);
            if (existing is null)
            {
                existing = new ApplicationRole { Name = role, NormalizedName = role.ToUpperInvariant() };
                var create = await _roles.CreateAsync(existing);
                if (!create.Succeeded)
                {
                    throw new InvalidOperationException($"Cannot create role {role}: {string.Join(", ", create.Errors.Select(e => e.Description))}");
                }
            }

            var expected = role == RoleDefinitions.SuperAdmin
                ? Permissions.All.ToArray()
                : RoleDefinitions.RolePermissions.TryGetValue(role, out var list) ? list : Array.Empty<string>();
            await SyncRoleClaimsAsync(existing, expected);
        }
    }

    private async Task SyncRoleClaimsAsync(ApplicationRole role, IReadOnlyCollection<string> expectedPermissions)
    {
        var currentClaims = await _roles.GetClaimsAsync(role);
        var currentPermissions = currentClaims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var missing in expectedPermissions.Where(p => !currentPermissions.Contains(p)))
        {
            await _roles.AddClaimAsync(role, new Claim("permission", missing));
        }

        foreach (var stale in currentPermissions.Where(p => !expectedPermissions.Contains(p)))
        {
            var claim = currentClaims.First(c => c.Type == "permission" && c.Value == stale);
            await _roles.RemoveClaimAsync(role, claim);
        }
    }

    private async Task<Project> SeedProjectAsync(CancellationToken ct)
    {
        var seed = SeedData.QpacProject();
        var existing = await _db.Projects.FirstOrDefaultAsync(p => p.Code == seed.Code, ct);
        if (existing is not null)
        {
            return existing;
        }

        _db.Projects.Add(seed);
        await _db.SaveChangesAsync(ct);
        return seed;
    }

    private async Task SeedDisciplinesAsync(Guid projectId, CancellationToken ct)
    {
        var existingCodes = await _db.Disciplines
            .Where(d => d.ProjectId == projectId)
            .Select(d => d.Code)
            .ToListAsync(ct);
        var existing = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, corporateName) in SeedData.Disciplines)
        {
            if (existing.Contains(code))
            {
                continue;
            }

            _db.Disciplines.Add(new Discipline
            {
                ProjectId = projectId,
                Code = code,
                CorporateName = corporateName,
            });
        }
    }

    private async Task SeedStatusMappingsAsync(Guid projectId, CancellationToken ct)
    {
        var existingStatuses = await _db.StatusMappings
            .Where(m => m.ProjectId == projectId)
            .Select(m => m.AconexStatus)
            .ToListAsync(ct);
        var existing = existingStatuses.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (aconex, unified, isLegacy) in SeedData.StatusMappings)
        {
            if (existing.Contains(aconex))
            {
                continue;
            }

            _db.StatusMappings.Add(new StatusMapping
            {
                ProjectId = projectId,
                AconexStatus = aconex,
                Status = unified,
                IsLegacy = isLegacy,
            });
        }
    }

    // The dropdown values every screen needs, so a fresh database is usable before the
    // first Drive poll. Rows already present (including soft-deleted ones, which an
    // operator removed on purpose) are left alone; the Picklists workbook upserts the
    // same (Field, Code) keys later.
    private async Task SeedPicklistsAsync(Guid projectId, CancellationToken ct)
    {
        var existingKeys = await _db.PicklistItems
            .Where(p => p.ProjectId == projectId)
            .Select(p => new { p.Field, p.Code })
            .ToListAsync(ct);
        var existing = existingKeys
            .Select(k => (k.Field, k.Code.ToUpperInvariant()))
            .ToHashSet();

        var sortOrder = new Dictionary<PicklistField, int>();
        foreach (var (field, code, description) in SeedPicklists.Items)
        {
            sortOrder[field] = sortOrder.GetValueOrDefault(field) + 1;
            if (existing.Contains((field, code.ToUpperInvariant())))
            {
                continue;
            }

            _db.PicklistItems.Add(new PicklistItem
            {
                ProjectId = projectId,
                Field = field,
                Code = code,
                Description = description,
                SortOrder = sortOrder[field],
            });
        }
    }

    private async Task SeedSuperAdminAsync()
    {
        var email = _configuration["Seed:AdminEmail"];
        var password = _configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation("Seed:AdminEmail / Seed:AdminPassword not configured; skipping SuperAdmin seed");
            return;
        }

        var user = await _users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = "System Administrator",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            var create = await _users.CreateAsync(user, password);
            if (!create.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Cannot create SuperAdmin: {string.Join(", ", create.Errors.Select(e => e.Description))}");
            }
        }

        if (!await _users.IsInRoleAsync(user, RoleDefinitions.SuperAdmin))
        {
            await _users.AddToRoleAsync(user, RoleDefinitions.SuperAdmin);
        }
    }
}
