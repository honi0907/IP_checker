using System.Management;
using IPChecker.Models;

namespace IPChecker.Helpers;

internal static class UsbLanDetector
{
    internal sealed record PhysicalAdapter(
        uint Index,
        string Name,
        bool NetEnabled,
        ushort NetConnectionStatus,
        string? PnpDeviceId);

    internal static bool IsUsbLanCandidate(string name, string? description, string? pnpDeviceId, uint? adapterType)
    {
        if (string.IsNullOrWhiteSpace(pnpDeviceId)
            || !pnpDeviceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var label = $"{name} {description}";
        if (ContainsAny(label, "Wi-Fi", "Wireless", "WLAN", "802.11", "Bluetooth"))
        {
            return false;
        }

        if (adapterType == 71)
        {
            return false;
        }

        if (adapterType == 0)
        {
            return true;
        }

        return ContainsAny(
            label,
            "Ethernet",
            "イーサネット",
            "LAN",
            "GbE",
            "Gigabit",
            "ASIX",
            "AX88",
            "USB",
            "FE ",
            "1000M",
            "10/100");
    }

    internal static List<PhysicalAdapter> QueryPhysicalUsbLanAdapters()
    {
        var results = new List<PhysicalAdapter>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Index, Name, Description, PNPDeviceID, NetConnectionStatus, NetEnabled, PhysicalAdapter, AdapterType " +
                "FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE");

            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                var name = obj["Name"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var description = obj["Description"]?.ToString()?.Trim();
                var pnpDeviceId = obj["PNPDeviceID"]?.ToString()?.Trim();
                var adapterType = obj["AdapterType"] is uint type ? type : (uint?)null;

                if (!IsUsbLanCandidate(name, description, pnpDeviceId, adapterType))
                {
                    continue;
                }

                var displayName = string.IsNullOrWhiteSpace(description) ? name : description;
                var index = obj["Index"] is uint indexValue ? indexValue : 0;
                var netEnabled = obj["NetEnabled"] is true;
                var connectionStatus = obj["NetConnectionStatus"] is ushort status ? status : (ushort)0;

                results.Add(new PhysicalAdapter(index, displayName, netEnabled, connectionStatus, pnpDeviceId));
            }
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"USB LAN adapter query failed: {ex}");
        }

        return results;
    }

    internal static string MapLinkStatusLabel(ushort netConnectionStatus) => netConnectionStatus switch
    {
        2 => "リンク接続",
        3 => "接続中",
        4 => "切断中",
        7 => "ケーブル未接続",
        0 => "切断",
        _ => "状態不明"
    };

    internal static UsbLanStatus BuildStatus(IReadOnlyList<NetworkAdapterInfo> usbLanAdapters)
    {
        if (usbLanAdapters.Count == 0)
        {
            return UsbLanStatus.NotDetected;
        }

        var primary = usbLanAdapters
            .OrderByDescending(a => a.AssignmentMode == IpAssignmentMode.Static)
            .ThenByDescending(a => a.AssignmentMode == IpAssignmentMode.Dhcp)
            .ThenByDescending(a => a.UsbLanState == UsbLanRecognitionState.Recognized)
            .First();

        var shortName = ShortenName(primary.Name);
        var summary = primary.UsbLanState switch
        {
            UsbLanRecognitionState.Disabled => $"USB LAN: 認識済み（無効）",
            _ => $"USB LAN: 認識済み"
        };

        var detailParts = new List<string> { shortName };
        if (!string.IsNullOrWhiteSpace(primary.LinkStatusLabel))
        {
            detailParts.Add(primary.LinkStatusLabel);
        }

        if (primary.AssignmentMode == IpAssignmentMode.Static && !string.IsNullOrWhiteSpace(primary.IPv4Address))
        {
            detailParts.Add($"{primary.IPv4Address} 手動");
        }
        else if (primary.AssignmentMode == IpAssignmentMode.Dhcp && !string.IsNullOrWhiteSpace(primary.IPv4Address))
        {
            detailParts.Add($"{primary.IPv4Address} 自動");
        }
        else if (primary.AssignmentMode == IpAssignmentMode.NoIp)
        {
            detailParts.Add("IP未取得");
        }

        return new UsbLanStatus
        {
            IsDetected = true,
            Summary = summary,
            Detail = string.Join(" / ", detailParts)
        };
    }

    private static string ShortenName(string name)
    {
        if (name.Length <= 28)
        {
            return name;
        }

        return name[..28] + "…";
    }

    private static bool ContainsAny(string value, params string[] markers) =>
        markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
