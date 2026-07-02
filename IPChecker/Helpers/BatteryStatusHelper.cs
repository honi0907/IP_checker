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
