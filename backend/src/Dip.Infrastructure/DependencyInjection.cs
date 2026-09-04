using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.Infrastructure;

// Composition root for the infrastructure layer.
// Phase 1.1 only wires an empty extension so the API host can compile.
// Phase 1.2 will add DbContext, Identity, migrations; Phase 2.1 the Drive client.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        return services;
    }
}
