using System.Diagnostics;

namespace IPChecker.Helpers;

internal static class WindowsNetworkSettingsHelper
{
    public static bool TryOpenNetworkAndSharingCenter()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "control.exe",
                Arguments = "/name Microsoft.NetworkAndSharingCenter",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"Open Network and Sharing Center failed: {ex}");
            return false;
        }
    }
}
