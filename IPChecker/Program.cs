using Microsoft.UI.Xaml;

namespace IPChecker;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            WriteStartupLog($"AppDomain.UnhandledException: {e.ExceptionObject}");
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            WriteStartupLog("Process exiting.");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteStartupLog($"UnobservedTaskException: {e.Exception.GetBaseException()}");
            e.SetObserved();
        };

        try
        {
            if (!Helpers.SingleInstanceHelper.TryAcquire())
            {
                return;
            }

            WriteStartupLog("Single instance acquired.");
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Microsoft.UI.Xaml.Application.Start(_ => new App());
        }
        catch (Exception ex)
        {
            WriteStartupLog(ex.ToString());
            throw;
        }
        finally
        {
            Helpers.SingleInstanceHelper.Release();
        }
    }

    private static void WriteStartupLog(string message)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IPChecker");
            Directory.CreateDirectory(directory);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(directory, "startup-error.log"), line);
        }
        catch
        {
            // Best effort only.
        }
    }
}
