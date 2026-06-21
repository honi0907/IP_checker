using System.Runtime.InteropServices;

namespace IPChecker.Helpers;

public static class EfficiencyModeHelper
{
    private const int ProcessPowerThrottling = 4;
    private const uint ProcessPowerThrottlingExecutionSpeed = 0x1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(
        nint processHandle,
        int processInformationClass,
        ref ProcessPowerThrottlingState processInformation,
        uint processInformationSize);

    public static void SetEcoMode(bool enabled)
    {
        var state = new ProcessPowerThrottlingState
        {
            Version = 1,
            ControlMask = ProcessPowerThrottlingExecutionSpeed,
            StateMask = enabled ? ProcessPowerThrottlingExecutionSpeed : 0
        };

        _ = SetProcessInformation(
            System.Diagnostics.Process.GetCurrentProcess().Handle,
            ProcessPowerThrottling,
            ref state,
            (uint)Marshal.SizeOf<ProcessPowerThrottlingState>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessPowerThrottlingState
    {
        public uint Version;

        public uint ControlMask;

        public uint StateMask;
    }
}
