using System.Runtime.InteropServices;
using IPChecker.Models;

namespace IPChecker.Helpers;

internal static class BatteryStatusHelper
{
    private const byte BatteryFlagCharging = 8;
    private const byte BatteryFlagNoBattery = 128;
    private const byte BatteryFlagUnknown = 255;
    private const byte BatteryLifePercentUnknown = 255;
    private const byte AcLineOffline = 0;
    private const byte AcLineOnline = 1;
    private const byte AcLineUnknown = 255;

    public static Task<BatteryStatus?> TryGetStatusAsync()
    {
        try
        {
            if (!GetSystemPowerStatus(out var status))
            {
                return Task.FromResult<BatteryStatus?>(null);
            }

            if ((status.BatteryFlag & BatteryFlagNoBattery) != 0)
            {
                return Task.FromResult<BatteryStatus?>(null);
            }

            if (status.BatteryLifePercent == BatteryLifePercentUnknown)
            {
                return Task.FromResult<BatteryStatus?>(null);
            }

            var percent = Math.Clamp((int)status.BatteryLifePercent, 0, 100);
            var powerState = ResolvePowerState(status);
            return Task.FromResult<BatteryStatus?>(new BatteryStatus(percent, powerState));
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"BatteryStatusHelper failed: {ex.Message}");
            return Task.FromResult<BatteryStatus?>(null);
        }
    }

    private static BatteryPowerState ResolvePowerState(SystemPowerStatus status)
    {
        var isCharging = (status.BatteryFlag & BatteryFlagCharging) != 0;
        if (isCharging)
        {
            return BatteryPowerState.Charging;
        }

        var onAc = status.ACLineStatus == AcLineOnline;
        if (!onAc && status.ACLineStatus == AcLineUnknown && status.BatteryFlag == BatteryFlagUnknown)
        {
            return BatteryPowerState.Discharging;
        }

        return onAc ? BatteryPowerState.PluggedIn : BatteryPowerState.Discharging;
    }

    public static string GetBatteryIconGlyph(int percent, BatteryPowerState powerState)
    {
        var level = Math.Clamp((int)Math.Round(percent / 10.0), 0, 10);
        if (powerState == BatteryPowerState.Charging)
        {
            return level switch
            {
                0 => "\uE83E",
                1 => "\uE840",
                2 => "\uE841",
                3 => "\uE842",
                4 => "\uE843",
                5 => "\uE844",
                6 => "\uE845",
                7 => "\uE846",
                8 => "\uE847",
                9 => "\uE848",
                _ => "\uE86A",
            };
        }

        return level switch
        {
            0 => "\uE83F",
            1 => "\uE850",
            2 => "\uE851",
            3 => "\uE852",
            4 => "\uE853",
            5 => "\uE854",
            6 => "\uE855",
            7 => "\uE856",
            8 => "\uE857",
            9 => "\uE858",
            _ => "\uE859",
        };
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte Reserved1;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
}
