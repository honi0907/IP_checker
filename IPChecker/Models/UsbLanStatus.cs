namespace IPChecker.Models;

public sealed record UsbLanStatus
{
    public bool IsDetected { get; init; }

    public string Summary { get; init; } = "USB LAN: 未検出";

    public string Detail { get; init; } = string.Empty;

    public static UsbLanStatus NotDetected { get; } = new()
    {
        IsDetected = false,
        Summary = "USB LAN: 未検出",
        Detail = "USB 有線アダプタが見つかりません"
    };
}
