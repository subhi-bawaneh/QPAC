using Dip.Application.Abstractions;
using Dip.Infrastructure.Drive;
using Dip.Infrastructure.Identity;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required");

        services.AddDbContext<DipDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(DipDbContext).Assembly.GetName().Name);
                npgsql.EnableRetryOnFailure(3);
            });
        });

        // Application depends on the abstraction; concrete DbContext is registered separately above.
        services.AddScoped<IDipDbContext>(sp => sp.GetRequiredService<DipDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<DipDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.Configure<GoogleDriveOptions>(configuration.GetSection(GoogleDriveOptions.SectionName));
        var mirrorPath = configuration["GoogleDrive:LocalMirrorPath"];
        var isDevelopment = string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development",
            "Development", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(mirrorPath) && isDevelopment)
        {
            // A checked-out copy of the Drive tree stands in for Google on a developer
            // machine; "." names the mirror root the way a folder id names the real root.
            services.PostConfigure<GoogleDriveOptions>(o =>
            {
                if (string.IsNullOrWhiteSpace(o.RootFolderId)) o.RootFolderId = ".";
            });
            services.AddSingleton<IDriveClient, LocalMirrorDriveClient>();
        }
        else
        {
            services.AddHttpClient<IDriveClient, ApiKeyDriveClient>();
        }

        services.AddSingleton<IExcelReader, Excel.ClosedXmlReader>();
        services.AddSingleton<IReportExporter, Excel.ClosedXmlReportExporter>();

        // Register importers as scoped so they can accept DipDbContext.
        services.AddScoped<Importers.PicklistImporter>();
        services.AddScoped<Importers.BaselineImporter>();
        services.AddScoped<Importers.ListsImporter>();
        services.AddScoped<Importers.TidpImporter>();
        services.AddScoped<Importers.MidpImporter>();
        services.AddScoped<Importers.AconexHistoryImporter>();

        services.AddScoped<IdentitySeeder>();

        return services;
    }
}
