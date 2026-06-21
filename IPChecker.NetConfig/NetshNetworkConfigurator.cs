using System.Diagnostics;

namespace IPChecker.NetConfig;

internal static class NetshNetworkConfigurator
{
    public static NetworkConfigResult? TryApplyDhcp(string interfaceAlias, uint? interfaceIndex = null)
    {
        if (string.IsNullOrWhiteSpace(interfaceAlias) && interfaceIndex is null or 0)
        {
            return null;
        }

        var target = BuildInterfaceTarget(interfaceAlias, interfaceIndex);
        var addressResult = Run($"interface ipv4 set address {target} source=dhcp");
        if (!addressResult.Success)
        {
            return addressResult;
        }

        var dnsResult = Run($"interface ipv4 set dns {target} source=dhcp");
        return dnsResult.Success
            ? Success("DHCP（自動取得）に変更しました。")
            : addressResult;
    }

    public static NetworkConfigResult? TryApplyStatic(
        string interfaceAlias,
        string ip,
        string mask,
        string? gateway,
        uint? interfaceIndex = null)
    {
        if (string.IsNullOrWhiteSpace(interfaceAlias) && interfaceIndex is null or 0)
        {
            return null;
        }

        var target = BuildInterfaceTarget(interfaceAlias, interfaceIndex);
        var args = string.IsNullOrWhiteSpace(gateway)
            ? $"interface ipv4 set address {target} static {ip} {mask}"
            : $"interface ipv4 set address {target} static {ip} {mask} {gateway} 1";

        return Run(args);
    }

    private static NetworkConfigResult Run(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Fail(-1, "netsh を起動できませんでした。");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                return Success("ネットワーク設定を変更しました。");
            }

            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim();
            return Fail(process.ExitCode, string.IsNullOrWhiteSpace(detail)
                ? "netsh コマンドに失敗しました。"
                : $"netsh コマンドに失敗しました: {detail}");
        }
        catch (Exception ex)
        {
            return Fail(-1, $"netsh コマンドに失敗しました: {ex.Message}");
        }
    }

    private static string BuildInterfaceTarget(string interfaceAlias, uint? interfaceIndex) =>
        interfaceIndex is > 0
            ? $"interface={interfaceIndex}"
            : $"name=\"{Escape(interfaceAlias)}\"";

    private static string Escape(string value) => value.Replace("\"", "\\\"", StringComparison.Ordinal);

    private static NetworkConfigResult Success(string message) =>
        new() { Success = true, ReturnCode = 0, Message = message };

    private static NetworkConfigResult Fail(int returnCode, string message) =>
        new() { Success = false, ReturnCode = returnCode, Message = message };
}
