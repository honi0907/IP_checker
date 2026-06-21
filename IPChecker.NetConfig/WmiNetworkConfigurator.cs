using System.Management;
using System.Net;

namespace IPChecker.NetConfig;

internal static class WmiNetworkConfigurator
{
    private static readonly uint[] WmiFallbackCodes = [84, 97, 80, 81, 100];

    public static NetworkConfigResult Apply(NetworkConfigRequest request)
    {
        if (request.ConfigurationIndex == 0)
        {
            return Fail(unchecked((uint)-1), "アダプタ Index が無効です。");
        }

        var identifiers = GetAdapterIdentifiers(request.ConfigurationIndex);
        if (!AdapterNameMatches(identifiers.Description, request.ExpectedAdapterName))
        {
            return Fail(unchecked((uint)-1), "アダプタ名が一致しません。更新を中止しました。");
        }

        EnsureNetworkAdapterEnabled(request.ConfigurationIndex);
        WaitForIpEnabled(request.ConfigurationIndex, TimeSpan.FromSeconds(6));

        using var config = GetConfiguration(request.ConfigurationIndex);
        if (config is null)
        {
            return Fail(unchecked((uint)-1), $"Index {request.ConfigurationIndex} のアダプタ設定が見つかりません。");
        }

        if (string.Equals(request.Mode, "Dhcp", StringComparison.OrdinalIgnoreCase))
        {
            var dhcpResult = ApplyDhcp(config);
            return dhcpResult.Success || !ShouldTryFallback(dhcpResult.ReturnCode)
                ? dhcpResult
                : ApplyDhcpFallback(request.ConfigurationIndex, identifiers);
        }

        if (string.Equals(request.Mode, "Static", StringComparison.OrdinalIgnoreCase))
        {
            var staticResult = ApplyStatic(config, request);
            return staticResult.Success || !ShouldTryFallback(staticResult.ReturnCode)
                ? staticResult
                : ApplyStaticFallback(request, identifiers);
        }

        return Fail(unchecked((uint)-1), "不明なモードです。");
    }

    private static NetworkConfigResult ApplyDhcp(ManagementObject config)
    {
        var dhcpResult = InvokeReturnValue(config, "EnableDHCP", null);
        if (dhcpResult is not (0 or 1))
        {
            return Fail(dhcpResult, DescribeReturnValue(dhcpResult, "DHCP の有効化"));
        }

        return Success(dhcpResult, "DHCP（自動取得）に変更しました。");
    }

    private static NetworkConfigResult ApplyStatic(ManagementObject config, NetworkConfigRequest request)
    {
        if (!TryParseIPv4(request.IPv4Address, out var ip))
        {
            return Fail(unchecked((uint)-1), "IP アドレスが無効です。");
        }

        if (!TryParseIPv4(request.SubnetMask, out var mask))
        {
            return Fail(unchecked((uint)-1), "サブネットマスクが無効です。");
        }

        string? gateway = null;
        if (!string.IsNullOrWhiteSpace(request.DefaultGateway))
        {
            if (!TryParseIPv4(request.DefaultGateway, out gateway))
            {
                return Fail(unchecked((uint)-1), "デフォルトゲートウェイが無効です。");
            }
        }

        var staticResult = InvokeReturnValue(
            config,
            "EnableStatic",
            new Dictionary<string, object>
            {
                ["IPAddress"] = new[] { ip! },
                ["SubnetMask"] = new[] { mask! }
            });

        if (staticResult is not (0 or 1))
        {
            return Fail(staticResult, DescribeReturnValue(staticResult, "静的 IP の設定"));
        }

        if (!string.IsNullOrWhiteSpace(gateway))
        {
            var gatewayResult = InvokeReturnValue(
                config,
                "SetGateways",
                new Dictionary<string, object>
                {
                    ["DefaultIPGateway"] = new[] { gateway! },
                    ["GatewayCostMetric"] = new ushort[] { 1 }
                });

            if (gatewayResult is not (0 or 1))
            {
                return Fail(gatewayResult, DescribeReturnValue(gatewayResult, "ゲートウェイの設定"));
            }

            return Success(gatewayResult, "静的 IP とゲートウェイを設定しました。");
        }

        return Success(staticResult, "静的 IP を設定しました。");
    }

    private static NetworkConfigResult ApplyDhcpFallback(
        uint configurationIndex,
        AdapterIdentifiers identifiers)
    {
        var netshResult = NetshNetworkConfigurator.TryApplyDhcp(
            identifiers.NetConnectionId ?? string.Empty,
            identifiers.InterfaceIndex);

        if (netshResult is { Success: true })
        {
            RegistryNetworkConfigurator.TryApplyDhcp(identifiers.SettingId ?? string.Empty);
            return netshResult;
        }

        var registryResult = RegistryNetworkConfigurator.TryApplyDhcp(identifiers.SettingId ?? string.Empty);
        if (!registryResult.Success)
        {
            return netshResult ?? registryResult;
        }

        RestartNetworkAdapter(configurationIndex);
        WaitForIpEnabled(configurationIndex, TimeSpan.FromSeconds(4));

        netshResult = NetshNetworkConfigurator.TryApplyDhcp(
            identifiers.NetConnectionId ?? string.Empty,
            identifiers.InterfaceIndex);
        if (netshResult is { Success: true })
        {
            return netshResult;
        }

        using var config = GetConfiguration(configurationIndex);
        if (config is not null)
        {
            var retryResult = ApplyDhcp(config);
            if (retryResult.Success)
            {
                return retryResult;
            }
        }

        return new NetworkConfigResult
        {
            Success = true,
            ReturnCode = 0,
            Message = "DHCP（自動取得）を保存しました。Windows の表示はケーブル接続後に更新される場合があります。"
        };
    }

    private static NetworkConfigResult ApplyStaticFallback(
        NetworkConfigRequest request,
        AdapterIdentifiers identifiers)
    {
        if (!TryParseIPv4(request.IPv4Address, out var ip)
            || !TryParseIPv4(request.SubnetMask, out var mask))
        {
            return Fail(unchecked((uint)-1), "IP 設定が無効です。");
        }

        string? gateway = null;
        if (!string.IsNullOrWhiteSpace(request.DefaultGateway))
        {
            if (!TryParseIPv4(request.DefaultGateway, out gateway))
            {
                return Fail(unchecked((uint)-1), "デフォルトゲートウェイが無効です。");
            }
        }

        var netshResult = NetshNetworkConfigurator.TryApplyStatic(
            identifiers.NetConnectionId ?? string.Empty,
            ip!,
            mask!,
            gateway,
            identifiers.InterfaceIndex);

        if (netshResult is { Success: true })
        {
            RegistryNetworkConfigurator.TryApplyStatic(
                identifiers.SettingId ?? string.Empty,
                ip!,
                mask!,
                gateway);
            return netshResult;
        }

        var registryResult = RegistryNetworkConfigurator.TryApplyStatic(
            identifiers.SettingId ?? string.Empty,
            ip!,
            mask!,
            gateway);
        if (!registryResult.Success)
        {
            return netshResult ?? registryResult;
        }

        RestartNetworkAdapter(request.ConfigurationIndex);
        WaitForIpEnabled(request.ConfigurationIndex, TimeSpan.FromSeconds(4));

        netshResult = NetshNetworkConfigurator.TryApplyStatic(
            identifiers.NetConnectionId ?? string.Empty,
            ip!,
            mask!,
            gateway,
            identifiers.InterfaceIndex);
        if (netshResult is { Success: true })
        {
            return netshResult;
        }

        using var config = GetConfiguration(request.ConfigurationIndex);
        if (config is not null)
        {
            var retryRequest = new NetworkConfigRequest
            {
                ConfigurationIndex = request.ConfigurationIndex,
                ExpectedAdapterName = request.ExpectedAdapterName,
                Mode = "Static",
                IPv4Address = ip,
                SubnetMask = mask,
                DefaultGateway = gateway
            };
            var retryResult = ApplyStatic(config, retryRequest);
            if (retryResult.Success)
            {
                return retryResult;
            }
        }

        return new NetworkConfigResult
        {
            Success = true,
            ReturnCode = 0,
            Message = "静的 IP を保存しました。Windows の表示はケーブル接続後に更新される場合があります。"
        };
    }

    private static void RestartNetworkAdapter(uint configurationIndex)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Index FROM Win32_NetworkAdapter " +
                $"WHERE Index = {configurationIndex}");

            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                obj.InvokeMethod("Disable", null, null);
                Thread.Sleep(1500);
                obj.InvokeMethod("Enable", null, null);
                return;
            }
        }
        catch
        {
            // 再起動に失敗しても他の手段で反映される場合がある。
        }
    }

    private static ManagementObject? GetConfiguration(uint index)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Index, Caption, Description, IPEnabled, SettingID FROM Win32_NetworkAdapterConfiguration " +
            $"WHERE Index = {index}");

        foreach (var obj in searcher.Get().Cast<ManagementObject>())
        {
            return obj;
        }

        return null;
    }

    private static AdapterIdentifiers GetAdapterIdentifiers(uint index)
    {
        string? description = null;
        string? netConnectionId = null;
        string? settingId = null;
        uint? interfaceIndex = null;

        try
        {
            using var adapterSearcher = new ManagementObjectSearcher(
                "SELECT Description, NetConnectionID, InterfaceIndex FROM Win32_NetworkAdapter " +
                $"WHERE Index = {index}");

            foreach (var obj in adapterSearcher.Get().Cast<ManagementObject>())
            {
                description = obj["Description"]?.ToString()?.Trim();
                netConnectionId = obj["NetConnectionID"]?.ToString()?.Trim();
                interfaceIndex = obj["InterfaceIndex"] is uint ifIndex ? ifIndex : null;
                break;
            }
        }
        catch
        {
            // Ignore and continue with configuration query.
        }

        try
        {
            using var configSearcher = new ManagementObjectSearcher(
                "SELECT Description, SettingID FROM Win32_NetworkAdapterConfiguration " +
                $"WHERE Index = {index}");

            foreach (var obj in configSearcher.Get().Cast<ManagementObject>())
            {
                description ??= obj["Description"]?.ToString()?.Trim();
                settingId = obj["SettingID"]?.ToString()?.Trim();
                break;
            }
        }
        catch
        {
            // Ignore.
        }

        return new AdapterIdentifiers(description, netConnectionId, settingId, interfaceIndex);
    }

    private static bool AdapterNameMatches(string? actualName, string expectedName)
    {
        var actual = actualName?.Trim();
        return !string.IsNullOrWhiteSpace(actual)
            && string.Equals(actual, expectedName, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureNetworkAdapterEnabled(uint configurationIndex)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Index, NetEnabled FROM Win32_NetworkAdapter " +
                $"WHERE Index = {configurationIndex}");

            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                if (obj["NetEnabled"] is true)
                {
                    return;
                }

                using var outParams = obj.InvokeMethod("Enable", null, null);
                _ = outParams?["ReturnValue"];
            }
        }
        catch
        {
            // 有効化に失敗しても後続のフォールバックを試す。
        }
    }

    private static bool WaitForIpEnabled(uint configurationIndex, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            using var config = GetConfiguration(configurationIndex);
            if (config?["IPEnabled"] is true)
            {
                return true;
            }

            Thread.Sleep(200);
        }

        return false;
    }

    private static bool ShouldTryFallback(int returnCode) =>
        returnCode < 0 || WmiFallbackCodes.Contains((uint)returnCode);

    private static uint InvokeReturnValue(
        ManagementObject config,
        string methodName,
        Dictionary<string, object>? parameters)
    {
        using var inParams = config.GetMethodParameters(methodName);
        if (parameters is not null)
        {
            foreach (var pair in parameters)
            {
                inParams[pair.Key] = pair.Value;
            }
        }

        using var outParams = config.InvokeMethod(methodName, inParams, null);
        return outParams?["ReturnValue"] is uint value ? value : 99u;
    }

    private static bool TryParseIPv4(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!IPAddress.TryParse(value.Trim(), out var parsed)
            || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        normalized = parsed.ToString();
        return true;
    }

    private static NetworkConfigResult Success(uint returnCode, string message) =>
        new() { Success = true, ReturnCode = (int)returnCode, Message = message };

    private static NetworkConfigResult Fail(uint returnCode, string message) =>
        new() { Success = false, ReturnCode = (int)returnCode, Message = message };

    private static string DescribeReturnValue(uint returnCode, string operation) => returnCode switch
    {
        0 or 1 => $"{operation}に成功しました。",
        70 or 91 => $"{operation}に失敗しました（アクセスが拒否されました）。",
        83 or 93 => $"{operation}に失敗しました（既に同じ設定です）。",
        84 => $"{operation}に失敗しました（アダプタで IP が未有効です。ケーブル未接続の可能性があります）。",
        97 => $"{operation}に失敗しました（このインターフェースは設定できません）。",
        100 => $"{operation}に失敗しました（DHCP が未有効です）。",
        _ => $"{operation}に失敗しました（コード: {returnCode}）。"
    };

    private sealed record AdapterIdentifiers(
        string? Description,
        string? NetConnectionId,
        string? SettingId,
        uint? InterfaceIndex);
}
