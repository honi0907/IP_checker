using System.Reflection;

namespace IPChecker.Helpers;

internal static class AppVersionHelper
{
    public static string DisplayVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version is null)
            {
                return "—";
            }

            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static string DisplayVersionText => $"v{DisplayVersion}";
}
