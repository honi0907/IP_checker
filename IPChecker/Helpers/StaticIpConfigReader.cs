using Microsoft.Win32;

namespace IPChecker.Helpers;

internal static class StaticIpConfigReader
{
    private const string TcpIpInterfacesKeyPath =
        @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    internal static bool? TryReadConfiguredDhcpEnabled(string? settingId)
    {
        if (string.IsNullOrWhiteSpace(settingId))
        {
            return null;
        }

        try
        {
            using var interfacesKey = Registry.LocalMachine.OpenSubKey(TcpIpInterfacesKeyPath);
            using var key = interfacesKey?.OpenSubKey(settingId);
            if (key is null)
            {
                return null;
            }

            return key.GetValue("EnableDHCP") is int dhcpEnabled
                ? dhcpEnabled != 0
                : null;
        }
        catch
        {
            return null;
        }
    }

    internal static string? TryReadConfiguredIPv4(string? settingId)
    {
        if (string.IsNullOrWhiteSpace(settingId))
        {
            return null;
        }

        try
        {
            using var interfacesKey = Registry.LocalMachine.OpenSubKey(TcpIpInterfacesKeyPath);
            using var key = interfacesKey?.OpenSubKey(settingId);
            if (key is null)
            {
                return null;
            }

            if (key.GetValue("EnableDHCP") is int dhcpEnabled && dhcpEnabled != 0)
            {
                return null;
            }

            return ExtractFirstIPv4(key.GetValue("IPAddress"));
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"Static IP registry read failed ({settingId}): {ex.Message}");
            return null;
        }
    }

    internal static string? TryReadConfiguredSubnetMask(string? settingId)
    {
        if (string.IsNullOrWhiteSpace(settingId))
        {
            return null;
        }

        try
        {
            using var interfacesKey = Registry.LocalMachine.OpenSubKey(TcpIpInterfacesKeyPath);
            using var key = interfacesKey?.OpenSubKey(settingId);
            if (key is null)
            {
                return null;
            }

            return ExtractFirstIPv4(key.GetValue("SubnetMask"));
        }
        catch
        {
            return null;
        }
    }

    internal static string? TryReadConfiguredGateway(string? settingId)
    {
        if (string.IsNullOrWhiteSpace(settingId))
        {
            return null;
        }

        try
        {
            using var interfacesKey = Registry.LocalMachine.OpenSubKey(TcpIpInterfacesKeyPath);
            using var key = interfacesKey?.OpenSubKey(settingId);
            if (key is null)
            {
                return null;
            }

            return ExtractFirstIPv4(key.GetValue("DefaultGateway"));
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractFirstIPv4(object? value) => value switch
    {
        string[] addresses => addresses.FirstOrDefault(IsValidConfiguredIPv4),
        string address when IsValidConfiguredIPv4(address) => address,
        _ => null
    };

    private static bool IsValidConfiguredIPv4(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        if (!System.Net.IPAddress.TryParse(address, out var parsed))
        {
            return false;
        }

        return parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && !System.Net.IPAddress.IsLoopback(parsed)
            && !parsed.Equals(System.Net.IPAddress.Any);
    }
}
