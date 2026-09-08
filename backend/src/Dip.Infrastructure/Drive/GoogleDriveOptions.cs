namespace Dip.Infrastructure.Drive;

public sealed class GoogleDriveOptions
{
    public const string SectionName = "GoogleDrive";

    public string ApiKey { get; set; } = string.Empty;
    public string RootFolderId { get; set; } = string.Empty;

    // Development only: a local copy of the Drive tree served through
    // LocalMirrorDriveClient instead of the Google API. Empty in every other environment.
    public string LocalMirrorPath { get; set; } = string.Empty;

    // Files.list default page size is 100; 1000 is the API max.
    public int PageSize { get; set; } = 1000;

    // How long a single files.list / files.get is allowed to take.
    public int RequestTimeoutSeconds { get; set; } = 60;

    // How often DriveSyncWorker polls. 0 or less disables the timer (tests).
    public double PollHours { get; set; } = 5;

    // Grace period before the first poll so the host finishes booting first.
    public int StartupDelaySeconds { get; set; } = 30;
}
