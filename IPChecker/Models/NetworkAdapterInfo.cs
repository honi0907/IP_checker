namespace IPChecker.Models;

public sealed record NetworkAdapterInfo
{
    public required string Name { get; init; }

    public string? IPv4Address { get; init; }

    public string? DefaultGateway { get; init; }

    public IpAssignmentMode AssignmentMode { get; init; }

    public bool IsConnected { get; init; }

    public bool IsPrimary { get; init; }

    public bool IsVirtual { get; init; }

    public bool IsUsbLan { get; init; }

    public UsbLanRecognitionState UsbLanState { get; init; }

    public string? LinkStatusLabel { get; init; }

    public uint ConfigurationIndex { get; init; }

    public string? SettingId { get; init; }

    public string? SubnetMask { get; init; }

    public string? DnsServers { get; init; }

    public string? WifiSsid { get; init; }

    public string SnapshotKey =>
        $"{Name}|{IPv4Address}|{AssignmentMode}|{IsPrimary}|{DefaultGateway}|{IsUsbLan}|{UsbLanState}|{LinkStatusLabel}|{ConfigurationIndex}|{SubnetMask}|{WifiSsid}";

    public bool HasSameStatusAs(NetworkAdapterInfo other) =>
        SnapshotKey == other.SnapshotKey;
}
