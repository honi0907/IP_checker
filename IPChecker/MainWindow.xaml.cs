using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using IPChecker.Helpers;
using IPChecker.Models;

namespace IPChecker;

public sealed partial class MainWindow : Window
{
    private bool _isExiting;
    private bool _isApplyingPosition;

    public IRelayCommand ToggleWindowCommand { get; }

    public IRelayCommand ShowWindowCommand { get; }

    public IRelayCommand SettingsCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand CheckForUpdatesCommand { get; }

    public IRelayCommand ExitApplicationCommand { get; }

    public IRelayCommand ControllerTestCommand { get; }

    public MainWindow()
    {
        ToggleWindowCommand = new RelayCommand(ToggleVisibility);
        ShowWindowCommand = new RelayCommand(() => WindowHelper.ShowFromTray(this));
        SettingsCommand = new RelayCommand(OpenSettingsFlyout);
        ControllerTestCommand = new RelayCommand(OpenControllerTest);
        RefreshCommand = new AsyncRelayCommand(() => App.MainViewModel.RefreshAsync());
        CheckForUpdatesCommand = App.MainViewModel.CheckForUpdatesCommand;
        ExitApplicationCommand = new RelayCommand(App.Shutdown);

        InitializeComponent();

        WindowBackdropHelper.Apply(this, RootGrid, RootFrame);

        ExtendsContentIntoTitleBar = false;
        AppWindow.Title = "IP Checker";

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
        }

        if (AppAssetHelper.AssetExists("AppIcon.ico"))
        {
            AppWindow.SetIcon(AppAssetHelper.GetAppIconPath());
            TrayIcon.IconSource = AppAssetHelper.GetImageSource("AppIcon.ico");
        }
        else
        {
            App.WriteStartupLog($"App icon missing: {AppAssetHelper.GetAppIconPath()}");
        }

        RootFrame.Navigate(typeof(MainPage));
        RootFrame.SizeChanged += OnRootFrameSizeChanged;

        App.MainViewModel.AlwaysOnTopChanged += OnAlwaysOnTopChanged;
        App.MainViewModel.AdapterCountChanged += OnAdapterCountChanged;
        App.MainViewModel.DisplayModeChanged += OnDisplayModeChanged;
        App.MainViewModel.WindowAnchorChanged += OnWindowAnchorChanged;
        App.MainViewModel.MinimizeToTrayRequested += OnMinimizeToTrayRequested;
        App.MainViewModel.SettingsRequested += OnSettingsRequested;
        App.MainViewModel.ControllerTestRequested += OnControllerTestRequested;
        App.MainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;

        AppWindow.Changed += OnAppWindowChanged;
        AppWindow.Closing += OnAppWindowClosing;
        Activated += OnWindowActivated;
    }

    public void UpdateDragRegions()
    {
        if (RootFrame.Content is MainPage mainPage)
        {
            mainPage.ApplyDragRegions();
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        Activated -= OnWindowActivated;

        try
        {
            TrayIcon.ForceCreate();
            UpdateTrayIcon(App.MainViewModel.TrayIconState);
            TrayIcon.ToolTipText = App.MainViewModel.TrayTooltipText;
            App.WriteStartupLog("Tray icon created.");
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"Tray icon creation failed: {ex}");
        }

        App.NetworkMonitor.SetWindowVisible(WindowHelper.IsVisible(this));
        _ = App.MainViewModel.RefreshBatteryAsync();
        UpdateDragRegions();
        App.WriteStartupLog("Window activated.");
    }

    public void ApplyInitialLayout()
    {
        ApplyPositionChange(() =>
        {
            WindowHelper.ApplyStartupSettings(this, SettingsHelper.DisplayMode, 0);
            WindowHelper.SetAlwaysOnTop(this, SettingsHelper.IsAlwaysOnTop);
        });

        UpdateDragRegions();
    }

    public void PrepareForShutdown()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;

        try
        {
            if (TrayIcon.IsCreated)
            {
                TrayIcon.Dispose();
            }
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"Tray dispose failed: {ex}");
        }

        try
        {
            if (!AppWindow.IsVisible)
            {
                AppWindow.Show(false);
            }
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"Show before close failed: {ex}");
        }
    }

    public void ToggleVisibility()
    {
        if (WindowHelper.IsVisible(this))
        {
            WindowHelper.HideToTray(this);
        }
        else
        {
            WindowHelper.ShowFromTray(this);
        }
    }

    public void OpenControllerTest()
    {
        if (RootFrame.Content is MainPage mainPage)
        {
            mainPage.ShowControllerTest();
        }
    }

    public void OpenSettingsFlyout()
    {
        if (RootFrame.Content is MainPage mainPage)
        {
            mainPage.ShowSettingsFlyout();
        }
    }

    private void OnRootFrameSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDragRegions();
    }

    private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.MainViewModel.TrayTooltipText))
        {
            UpdateTrayTooltip();
        }

        if (e.PropertyName == nameof(ViewModels.MainViewModel.TrayIconState))
        {
            UpdateTrayIcon(App.MainViewModel.TrayIconState);
        }

        if (e.PropertyName is nameof(ViewModels.MainViewModel.MiniModeVisibility)
            or nameof(ViewModels.MainViewModel.DetailModeVisibility)
            or nameof(ViewModels.MainViewModel.SettingsVisibility)
            or nameof(ViewModels.MainViewModel.ControllerTestVisibility)
            or nameof(ViewModels.MainViewModel.IsSettingsOpen)
            or nameof(ViewModels.MainViewModel.IsControllerTestOpen))
        {
            UpdateDragRegions();
        }
    }

    private void UpdateTrayTooltip()
    {
        if (!TrayIcon.IsCreated)
        {
            return;
        }

        TrayIcon.ToolTipText = App.MainViewModel.TrayTooltipText;
    }

    private void UpdateTrayIcon(Models.TrayIconState state)
    {
        if (!TrayIcon.IsCreated)
        {
            return;
        }

        if (!App.DispatcherQueue.HasThreadAccess)
        {
            App.DispatcherQueue.TryEnqueue(() => UpdateTrayIcon(state));
            return;
        }

        try
        {
            TrayIcon.IconSource = Helpers.TrayIconHelper.GetIcon(state);
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"Update tray icon failed: {ex}");
        }
    }

    private void OnAlwaysOnTopChanged(object? sender, bool isAlwaysOnTop)
    {
        WindowHelper.SetAlwaysOnTop(this, isAlwaysOnTop);
    }

    private void OnAdapterCountChanged(object? sender, int count)
    {
        if (App.MainViewModel.DisplayMode == DisplayMode.Detail)
        {
            ApplyPositionChange(() =>
                WindowHelper.ResizeForDisplayMode(this, DisplayMode.Detail, count));
        }

        UpdateDragRegions();
    }

    private void OnDisplayModeChanged(object? sender, DisplayMode mode)
    {
        ApplyPositionChange(() =>
            WindowHelper.ResizeForDisplayMode(this, mode, App.MainViewModel.Adapters.Count));

        UpdateDragRegions();
    }

    private void OnWindowAnchorChanged(object? sender, WindowAnchor anchor)
    {
        ApplyPositionChange(() => WindowHelper.ApplyWindowAnchor(this, anchor));
    }

    private void OnMinimizeToTrayRequested(object? sender, EventArgs e)
    {
        WindowHelper.HideToTray(this);
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        OpenSettingsFlyout();
    }

    private void OnControllerTestRequested(object? sender, EventArgs e)
    {
        OpenControllerTest();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_isApplyingPosition || !args.DidPositionChange || _isExiting)
        {
            return;
        }

        try
        {
            WindowHelper.SaveWindowPosition(this);
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"Save window position failed: {ex}");
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting)
        {
            WindowHelper.SaveWindowPosition(this);
            return;
        }

        args.Cancel = true;
        WindowHelper.HideToTray(this);
    }

    private void ApplyPositionChange(Action action)
    {
        _isApplyingPosition = true;
        try
        {
            action();
        }
        finally
        {
            _isApplyingPosition = false;
        }
    }
}
