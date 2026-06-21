namespace IPChecker.Models;

public sealed class NetworkSnapshot
{
    public required IReadOnlyList<NetworkAdapterInfo> Adapters { get; init; }

    public NetworkAdapterInfo? PrimaryAdapter { get; init; }

    public UsbLanStatus UsbLan { get; init; } = UsbLanStatus.NotDetected;

    public string Fingerprint =>
        $"{UsbLan.Summary}|{UsbLan.Detail}|{string.Join(";", Adapters.Select(a => a.SnapshotKey))}";
}
