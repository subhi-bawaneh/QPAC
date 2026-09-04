using Dip.Application.Abstractions;
using Dip.Infrastructure.Identity;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddScoped<IdentitySeeder>();

        return services;
    }
}
