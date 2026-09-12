using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dip.Api.Health;

// Can the API reach its database? Uses CanConnectAsync rather than a query, so a
// health poll costs one round trip and never touches application data.
public sealed class DatabaseHealthCheck : IHealthCheck
{
    public const string Name = "database";

    private readonly DipDbContext _db;

    public DatabaseHealthCheck(DipDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database reachable")
                : HealthCheckResult.Unhealthy("Database did not accept a connection");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}
