using System.Reflection;

namespace IPChecker.Helpers;

internal static class AppVersionHelper
{
    public static Version? CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version;

    public static string DisplayVersion
    {
        get
        {
            var version = CurrentVersion;
            if (version is null)
            {
                return "—";
            }

            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static string DisplayVersionText => $"v{DisplayVersion}";

    public static string? NormalizeReleaseTag(string tag)
    {
        var trimmed = tag.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[1..];
        }

        return Version.TryParse(trimmed, out var version)
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : null;
    }

    public static bool IsRemoteNewer(string remoteVersionText)
    {
        var current = CurrentVersion;
        var remote = Version.TryParse(remoteVersionText, out var parsed) ? parsed : null;
        if (current is null || remote is null)
        {
            return false;
        }

        return remote > current;
    }
}
