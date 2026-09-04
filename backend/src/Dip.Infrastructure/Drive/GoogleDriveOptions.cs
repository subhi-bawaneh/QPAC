namespace Dip.Infrastructure.Drive;

public sealed class GoogleDriveOptions
{
    public const string SectionName = "GoogleDrive";

    public string ApiKey { get; set; } = string.Empty;
    public string RootFolderId { get; set; } = string.Empty;

    // Files.list default page size is 100; 1000 is the API max.
    public int PageSize { get; set; } = 1000;

    // How long a single files.list / files.get is allowed to take.
    public int RequestTimeoutSeconds { get; set; } = 60;
}
