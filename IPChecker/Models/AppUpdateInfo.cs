namespace IPChecker.Models;

public sealed class AppUpdateInfo
{
    public required string LatestVersion { get; init; }

    public required string ReleasePageUrl { get; init; }

    public string? InstallerDownloadUrl { get; init; }

    public bool IsNewerThanCurrent { get; init; }

    public string PreferredDownloadUrl => InstallerDownloadUrl ?? ReleasePageUrl;
}
