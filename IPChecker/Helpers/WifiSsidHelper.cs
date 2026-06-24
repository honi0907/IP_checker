using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace IPChecker.Helpers;

internal static class WifiSsidHelper
{
    private static readonly Regex SsidLineRegex = new(
        @"^\s*SSID\s*:\s*(.*)$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public static bool IsWifiAdapter(string name) =>
        name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Wireless", StringComparison.OrdinalIgnoreCase)
        || name.Contains("WLAN", StringComparison.OrdinalIgnoreCase)
        || name.Contains("802.11", StringComparison.OrdinalIgnoreCase);

    public static string? TryGetConnectedSsid()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                },
            };

            if (!process.Start())
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort timeout handling.
                }

                return null;
            }

            return ParseConnectedSsid(output);
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"WifiSsidHelper failed: {ex.Message}");
            return null;
        }
    }

    private static string? ParseConnectedSsid(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var connectedIndex = output.IndexOf("connected", StringComparison.OrdinalIgnoreCase);
        if (connectedIndex < 0)
        {
            connectedIndex = output.IndexOf("接続", StringComparison.Ordinal);
        }

        if (connectedIndex < 0)
        {
            return null;
        }

        var segment = output[connectedIndex..];
        var match = SsidLineRegex.Match(segment);
        if (!match.Success)
        {
            return null;
        }

        var ssid = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(ssid) ? null : ssid;
    }
}
