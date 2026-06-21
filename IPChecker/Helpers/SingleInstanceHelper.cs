using System.Runtime.InteropServices;

namespace IPChecker.Helpers;

public static class SingleInstanceHelper
{
    private const string MutexName = "Global\\IPChecker_SingleInstance_v1";
    private const int SwShow = 5;

    private static Mutex? _mutex;
    private static bool _ownsMutex;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        _ownsMutex = createdNew;

        if (createdNew)
        {
            return true;
        }

        ActivateExistingWindow();
        return false;
    }

    public static void Release()
    {
        if (_mutex is null)
        {
            return;
        }

        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _mutex = null;
        _ownsMutex = false;
    }

    private static void ActivateExistingWindow()
    {
        var hwnd = FindWindow(null, "IP Checker");

        if (hwnd == nint.Zero)
        {
            return;
        }

        ShowWindow(hwnd, SwShow);
        SetForegroundWindow(hwnd);
    }
}
