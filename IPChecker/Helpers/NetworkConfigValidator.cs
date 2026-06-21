using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using IPChecker.Models;

namespace IPChecker.Helpers;

internal static class NetworkConfigValidator
{
    public static string? Validate(NetworkConfigRequest request)
    {
        if (request.ConfigurationIndex == 0)
        {
            return "このアダプタは IP 設定を変更できません。";
        }

        if (string.IsNullOrWhiteSpace(request.ExpectedAdapterName))
        {
            return "アダプタ名が不明です。";
        }

        if (string.Equals(request.Mode, "Dhcp", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.Equals(request.Mode, "Static", StringComparison.OrdinalIgnoreCase))
        {
            return "設定モードが無効です。";
        }

        if (!TryParseIPv4(request.IPv4Address, out var ip))
        {
            return "IP アドレスを入力してください。";
        }

        if (!TryParseIPv4(request.SubnetMask, out var mask))
        {
            return "サブネットマスクを入力してください。";
        }

        if (!string.IsNullOrWhiteSpace(request.DefaultGateway))
        {
            if (!TryParseIPv4(request.DefaultGateway, out var gateway))
            {
                return "デフォルトゲートウェイが無効です。";
            }

            if (!IsSameSubnet(ip!, mask!, gateway!))
            {
                return "ゲートウェイが IP アドレスと同じサブネットにありません。";
            }
        }

        return null;
    }

    private static bool TryParseIPv4(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!IPAddress.TryParse(value.Trim(), out var parsed)
            || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        normalized = parsed.ToString();
        return true;
    }

    private static bool IsSameSubnet(string ip, string mask, string gateway)
    {
        if (!IPAddress.TryParse(ip, out var ipAddress)
            || !IPAddress.TryParse(mask, out var maskAddress)
            || !IPAddress.TryParse(gateway, out var gatewayAddress))
        {
            return false;
        }

        var ipBytes = ipAddress.GetAddressBytes();
        var maskBytes = maskAddress.GetAddressBytes();
        var gatewayBytes = gatewayAddress.GetAddressBytes();

        for (var i = 0; i < 4; i++)
        {
            if ((ipBytes[i] & maskBytes[i]) != (gatewayBytes[i] & maskBytes[i]))
            {
                return false;
            }
        }

        return true;
    }
}
