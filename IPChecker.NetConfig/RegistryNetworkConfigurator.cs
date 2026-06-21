using Microsoft.Win32;

namespace IPChecker.NetConfig;

internal static class RegistryNetworkConfigurator
{
    private const string TcpIpInterfacesKeyPath =
        @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    public static NetworkConfigResult TryApplyDhcp(string settingId)
    {
        if (!TryOpenInterfaceKey(settingId, out var key) || key is null)
        {
            return Fail("インターフェース設定キーが見つかりません。");
        }

        using (key)
        {
            key.SetValue("EnableDHCP", 1, RegistryValueKind.DWord);
            DeleteValueIfExists(key, "IPAddress");
            DeleteValueIfExists(key, "SubnetMask");
            DeleteValueIfExists(key, "DefaultGateway");
            DeleteValueIfExists(key, "DefaultGatewayMetric");
            DeleteValueIfExists(key, "NameServer");
        }

        return Success("DHCP（自動取得）を保存しました。");
    }

    public static NetworkConfigResult TryApplyStatic(
        string settingId,
        string ip,
        string mask,
        string? gateway)
    {
        if (!TryOpenInterfaceKey(settingId, out var key) || key is null)
        {
            return Fail("インターフェース設定キーが見つかりません。");
        }

        using (key)
        {
            key.SetValue("EnableDHCP", 0, RegistryValueKind.DWord);
            key.SetValue("IPAddress", new[] { ip }, RegistryValueKind.MultiString);
            key.SetValue("SubnetMask", new[] { mask }, RegistryValueKind.MultiString);

            if (!string.IsNullOrWhiteSpace(gateway))
            {
                key.SetValue("DefaultGateway", new[] { gateway }, RegistryValueKind.MultiString);
                key.SetValue("DefaultGatewayMetric", new[] { 1 }, RegistryValueKind.MultiString);
            }
            else
            {
                DeleteValueIfExists(key, "DefaultGateway");
                DeleteValueIfExists(key, "DefaultGatewayMetric");
            }
        }

        return Success("静的 IP を保存しました。");
    }

    private static bool TryOpenInterfaceKey(string settingId, out RegistryKey? key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(settingId))
        {
            return false;
        }

        try
        {
            using var interfacesKey = Registry.LocalMachine.OpenSubKey(TcpIpInterfacesKeyPath);
            key = interfacesKey?.OpenSubKey(settingId, writable: true);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    private static void DeleteValueIfExists(RegistryKey key, string valueName)
    {
        try
        {
            if (key.GetValue(valueName) is not null)
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    private static NetworkConfigResult Success(string message) =>
        new() { Success = true, ReturnCode = 0, Message = message };

    private static NetworkConfigResult Fail(string message) =>
        new() { Success = false, ReturnCode = -1, Message = message };
}
