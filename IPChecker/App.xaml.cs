using Microsoft.UI.Xaml;
using IPChecker.Helpers;
using IPChecker.Services;
using IPChecker.ViewModels;

namespace IPChecker;

public partial class App : Application
{
    public static Window Window { get; private set; } = null!;

    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    public static MainViewModel MainViewModel { get; private set; } = null!;

    public static INetworkMonitorService NetworkMonitor { get; private set; } = null!;

    public static IGameControllerService GameController { get; private set; } = null!;

    private static bool _isShuttingDown;

    public App()
    {
        UnhandledException += (_, e) =>
        {
            WriteStartupLog($"Unhandled: {e.Exception}");
            e.Handled = true;
        };
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            Launch();
        }
        catch (Exception ex)
        {
            WriteStartupLog(ex.ToString());
            throw;
        }
    }

    private void Launch()
    {
        WriteStartupLog("Launch started.");
        WriteStartupLog($"BaseDirectory: {AppContext.BaseDirectory}");
        WriteStartupLog($"AppIcon exists: {AppAssetHelper.AssetExists("AppIcon.ico")}");

        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        NetworkMonitor = new NetworkMonitorService();

        try
        {
            GameController = new GameControllerService(DispatcherQueue);
        }
        catch (Exception ex)
        {
            WriteStartupLog($"GameController init failed: {ex}");
            GameController = new NullGameControllerService();
        }

        MainViewModel = new MainViewModel(
            NetworkMonitor,
            GameController,
            new GitHubUpdateCheckService());
        MainViewModel.ExitRequested += OnExitRequested;

        Window = new MainWindow();

        if (Window is MainWindow mainWindow)
        {
            mainWindow.ApplyInitialLayout();
        }

        Window.Activate();

        if (SettingsHelper.StartInTrayOnly)
        {
            NetworkMonitor.SetWindowVisible(false);
            Window.AppWindow.Hide();
        }
        else
        {
            NetworkMonitor.SetWindowVisible(true);
        }

        WriteStartupLog("Launch completed.");
    }

    internal static void WriteStartupLog(string message)
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IPChecker");
        Directory.CreateDirectory(logDirectory);
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        File.AppendAllText(Path.Combine(logDirectory, "startup-error.log"), line);
    }

    internal static void Shutdown()
    {
        if (_isShuttingDown)
        {
            return;
        }

        if (!DispatcherQueue.HasThreadAccess)
        {
            if (!DispatcherQueue.TryEnqueue(Shutdown))
            {
                WriteStartupLog("Shutdown enqueue failed; forcing exit.");
                Environment.Exit(0);
            }

            return;
        }

        _isShuttingDown = true;

        try
        {
            NetworkMonitor?.Dispose();
            GameController?.Dispose();
        }
        catch (Exception ex)
        {
            WriteStartupLog($"Shutdown monitor dispose failed: {ex}");
        }

        if (Window is MainWindow mainWindow)
        {
            mainWindow.PrepareForShutdown();
            mainWindow.Close();
        }

        if (Current is App app)
        {
            app.Exit();
        }

        // Tray 経由の終了でメッセージループが残る場合の保険
        Environment.Exit(0);
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        Shutdown();
    }
}
