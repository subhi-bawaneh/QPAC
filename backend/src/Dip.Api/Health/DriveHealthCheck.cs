using Dip.Api.Workers;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Drive;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Dip.Api.Health;

// Can the API read the configured Drive root?
//
// Deliberately NOT part of the endpoint an uptime monitor polls: it spends Google
// API quota and depends on a third party, so a Drive outage must not make the API
// look down. It is registered under the "external" tag and exposed on /health/detail.
//
// Drive being unconfigured is Degraded, not Unhealthy — the platform runs perfectly
// well on uploaded workbooks alone.
public sealed class DriveHealthCheck : IHealthCheck
{
    public const string Name = "drive";

    private readonly IDriveClient _drive;
    private readonly GoogleDriveOptions _options;
    private readonly WorkerState _workers;

    public DriveHealthCheck(IDriveClient drive, IOptions<GoogleDriveOptions> options, WorkerState workers)
    {
        _drive = drive;
        _options = options.Value;
        _workers = workers;
    }

    private static string Describe(DateTime? at) =>
        at?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? "never";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.RootFolderId))
        {
            return HealthCheckResult.Degraded(
                "Google Drive is not configured; sync is unavailable but uploads work");
        }

        try
        {
            var children = await _drive.ListChildrenAsync(_options.RootFolderId, cancellationToken);
            return HealthCheckResult.Healthy(
                $"Drive root readable ({children.Count} entries); "
                + $"lastRunFinishedAt={Describe(_workers.LastRunFinishedAt)}; "
                + $"queuedImports={_workers.QueuedImports}");
        }
        catch (Exception ex)
        {
            // The client already rewords 403/404 into "not shared with Anyone with the
            // link" / "check the folder id", which is the message worth surfacing here.
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}
