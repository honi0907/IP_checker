using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPChecker.Helpers;
using IPChecker.Models;
using IPChecker.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace IPChecker.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INetworkMonitorService _networkMonitor;
    private readonly IUpdateCheckService _updateCheckService;
    private readonly Dictionary<string, NetworkAdapterItemViewModel> _adapterLookup = new(StringComparer.OrdinalIgnoreCase);
    private string _trayTooltipText = "IP Checker";
    private string _lastUpdatedText = string.Empty;
    private string _usbLanSummaryText = UsbLanStatus.NotDetected.Summary;
    private string _usbLanDetailText = UsbLanStatus.NotDetected.Detail;
    private bool _isAlwaysOnTop = true;
    private bool _runAtStartup;
    private bool _startInTrayOnly;
    private bool _showVirtualAdapters;
    private int _startupDelaySeconds;
    private WindowAnchor _selectedWindowAnchor = WindowAnchor.TopRight;
    private bool _enableEfficiencyMode = true;
    private bool _miniRotateAdapters = true;
    private TrayIconState _trayIconState = TrayIconState.NoIp;
    private bool _hasAdapters;
    private Visibility _emptyStateVisibility = Visibility.Visible;
    private Visibility _miniModeVisibility = Visibility.Visible;
    private Visibility _detailModeVisibility = Visibility.Collapsed;
    private Visibility _settingsVisibility = Visibility.Collapsed;
    private Visibility _controllerTestVisibility = Visibility.Collapsed;
    private bool _isSettingsOpen;
    private bool _isControllerTestOpen;
    private string _lastFingerprint = string.Empty;
    private NetworkAdapterItemViewModel _primaryAdapter = new(new NetworkAdapterInfo
    {
        Name = "—",
        AssignmentMode = IpAssignmentMode.NoIp
    });
    private NetworkAdapterItemViewModel _miniDisplayedAdapter = new(new NetworkAdapterInfo
    {
        Name = "—",
        AssignmentMode = IpAssignmentMode.NoIp
    });
    private readonly List<NetworkAdapterItemViewModel> _miniRotationSlots = [];
    private int _miniRotationIndex;
    private DispatcherQueueTimer? _miniRotationTimer;
    private DispatcherQueueTimer? _batteryTimer;
    private int? _batteryPercent;
    private BatteryPowerState _batteryPowerState = BatteryPowerState.Discharging;
    private bool _isStartingUp;
    private string _updateStatusText = "未確認";
    private bool _isUpdateAvailable;
    private bool _isCheckingForUpdates;
    private bool _isInstallingUpdate;
    private string? _updateDownloadUrl;

    public MainViewModel(
        INetworkMonitorService networkMonitor,
        IGameControllerService gameControllerService,
        IUpdateCheckService updateCheckService)
    {
        _networkMonitor = networkMonitor;
        _updateCheckService = updateCheckService;
        _networkMonitor.SnapshotChanged += OnSnapshotChanged;
        ControllerTest = new ControllerTestViewModel(gameControllerService);
    }

    public ControllerTestViewModel ControllerTest { get; }

    public ObservableCollection<NetworkAdapterItemViewModel> Adapters { get; } = [];

    public ObservableCollection<WindowAnchorOption> WindowAnchorOptions { get; } =
    [
        new WindowAnchorOption { Label = "右上", Value = WindowAnchor.TopRight },
        new WindowAnchorOption { Label = "左上", Value = WindowAnchor.TopLeft },
        new WindowAnchorOption { Label = "右下", Value = WindowAnchor.BottomRight },
        new WindowAnchorOption { Label = "左下", Value = WindowAnchor.BottomLeft },
        new WindowAnchorOption { Label = "自由（ドラッグ位置）", Value = WindowAnchor.Custom }
    ];

    public NetworkAdapterItemViewModel PrimaryAdapter => _primaryAdapter;

    public NetworkAdapterItemViewModel MiniDisplayedAdapter => _miniDisplayedAdapter;

    public string MiniRotationIndicatorText =>
        MiniRotateAdapters && _miniRotationSlots.Count > 1
            ? string.Concat(Enumerable.Range(0, _miniRotationSlots.Count)
                .Select(i => i == _miniRotationIndex ? '●' : '○'))
            : string.Empty;

    public Visibility MiniRotationIndicatorVisibility =>
        MiniRotateAdapters && _miniRotationSlots.Count > 1 && DisplayMode == DisplayMode.Mini
            ? Visibility.Visible
            : Visibility.Collapsed;

    public bool IsStartingUp
    {
        get => _isStartingUp;
        private set => SetProperty(ref _isStartingUp, value);
    }

    public string BatteryPercentText =>
        _batteryPercent.HasValue ? $"{_batteryPercent.Value}%" : string.Empty;

    public string BatteryIconGlyph =>
        _batteryPercent.HasValue
            ? BatteryStatusHelper.GetBatteryIconGlyph(_batteryPercent.Value, _batteryPowerState)
            : "\uE83D";

    public Brush BatteryIconForeground =>
        GetThemeBrush(
            _batteryPowerState switch
            {
                BatteryPowerState.Charging => "AccentTextFillColorPrimaryBrush",
                BatteryPowerState.PluggedIn => "SystemFillColorSuccessBrush",
                _ => IsLowBattery
                    ? "SystemFillColorCriticalBrush"
                    : "SystemFillColorCautionBrush",
            },
            Microsoft.UI.Colors.IndianRed);

    public Brush BatteryPercentForeground =>
        _batteryPowerState == BatteryPowerState.Discharging
            ? GetThemeBrush(
                IsLowBattery
                    ? "SystemFillColorCriticalBrush"
                    : "SystemFillColorCautionBrush",
                Microsoft.UI.Colors.DarkRed)
            : GetThemeBrush(
                "TextFillColorSecondaryBrush",
                Microsoft.UI.Colors.Gray);

    public Windows.UI.Text.FontWeight BatteryPercentFontWeight =>
        _batteryPowerState == BatteryPowerState.Discharging
            ? FontWeights.SemiBold
            : FontWeights.Normal;

    private bool IsLowBattery =>
        _batteryPercent is <= 20;

    public string BatteryTooltipText =>
        _batteryPercent.HasValue
            ? _batteryPowerState switch
            {
                BatteryPowerState.Charging => $"充電中 {_batteryPercent.Value}%",
                BatteryPowerState.PluggedIn => $"AC接続 {_batteryPercent.Value}%",
                _ => $"バッテリー {_batteryPercent.Value}%",
            }
            : string.Empty;

    public Visibility BatteryVisibility =>
        _batteryPercent.HasValue ? Visibility.Visible : Visibility.Collapsed;

    public string TrayTooltipText
    {
        get => _trayTooltipText;
        private set => SetProperty(ref _trayTooltipText, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetProperty(ref _lastUpdatedText, value);
    }

    public string AppVersionText => AppVersionHelper.DisplayVersionText;

    public string UpdateStatusText
    {
        get => _updateStatusText;
        private set => SetProperty(ref _updateStatusText, value);
    }

    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        private set
        {
            if (SetProperty(ref _isCheckingForUpdates, value))
            {
                OnPropertyChanged(nameof(CanCheckForUpdates));
            }
        }
    }

    public bool CanCheckForUpdates => !IsCheckingForUpdates && !IsInstallingUpdate;

    public bool IsInstallingUpdate
    {
        get => _isInstallingUpdate;
        private set
        {
            if (SetProperty(ref _isInstallingUpdate, value))
            {
                OnPropertyChanged(nameof(CanCheckForUpdates));
                OpenUpdateDownloadCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public Visibility UpdateDownloadVisibility =>
        _isUpdateAvailable ? Visibility.Visible : Visibility.Collapsed;

    public string UsbLanSummaryText
    {
        get => _usbLanSummaryText;
        private set => SetProperty(ref _usbLanSummaryText, value);
    }

    public string UsbLanDetailText
    {
        get => _usbLanDetailText;
        private set => SetProperty(ref _usbLanDetailText, value);
    }

    public string UsbLanStatusLine =>
        UsbLanDetailText.Length > 0
            ? $"{UsbLanSummaryText}  {UsbLanDetailText}"
            : UsbLanSummaryText;

    public Visibility UsbLanDetailVisibility =>
        string.IsNullOrWhiteSpace(UsbLanDetailText) ? Visibility.Collapsed : Visibility.Visible;

    public TrayIconState TrayIconState
    {
        get => _trayIconState;
        private set => SetProperty(ref _trayIconState, value);
    }

    public string TrayIconStateLabel => TrayIconState switch
    {
        TrayIconState.Dhcp => "自動 (DHCP)",
        TrayIconState.Static => "手動 (静的)",
        _ => "IP未取得"
    };

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set
        {
            if (SetProperty(ref _isAlwaysOnTop, value))
            {
                SettingsHelper.IsAlwaysOnTop = value;
                AlwaysOnTopChanged?.Invoke(this, value);
            }
        }
    }

    public bool RunAtStartup
    {
        get => _runAtStartup;
        set
        {
            if (SetProperty(ref _runAtStartup, value))
            {
                SettingsHelper.RunAtStartup = value;
                StartupHelper.SetEnabled(value);
            }
        }
    }

    public bool StartInTrayOnly
    {
        get => _startInTrayOnly;
        set
        {
            if (SetProperty(ref _startInTrayOnly, value))
            {
                SettingsHelper.StartInTrayOnly = value;
            }
        }
    }

    public bool ShowVirtualAdapters
    {
        get => _showVirtualAdapters;
        set
        {
            if (SetProperty(ref _showVirtualAdapters, value))
            {
                SettingsHelper.ShowVirtualAdapters = value;
                _ = RefreshAsync();
            }
        }
    }

    public int StartupDelaySeconds
    {
        get => _startupDelaySeconds;
        set
        {
            var clamped = Math.Clamp(value, 0, 120);
            if (SetProperty(ref _startupDelaySeconds, clamped))
            {
                SettingsHelper.StartupDelaySeconds = clamped;
            }
        }
    }

    public WindowAnchor SelectedWindowAnchor
    {
        get => _selectedWindowAnchor;
        set
        {
            if (SetProperty(ref _selectedWindowAnchor, value))
            {
                SettingsHelper.WindowAnchor = value;
                WindowAnchorChanged?.Invoke(this, value);
            }
        }
    }

    public bool EnableEfficiencyMode
    {
        get => _enableEfficiencyMode;
        set
        {
            if (SetProperty(ref _enableEfficiencyMode, value))
            {
                SettingsHelper.EnableEfficiencyMode = value;
                _networkMonitor.ReapplyEfficiencyProfile();
            }
        }
    }

    public bool MiniRotateAdapters
    {
        get => _miniRotateAdapters;
        set
        {
            if (SetProperty(ref _miniRotateAdapters, value))
            {
                SettingsHelper.MiniRotateAdapters = value;
                ApplyMiniDisplayedAdapter();
                SyncMiniRotationTimer();
            }
        }
    }

    public DisplayMode DisplayMode
    {
        get => _displayMode;
        private set
        {
            if (SetProperty(ref _displayMode, value))
            {
                ApplyViewVisibility();
                DisplayModeChanged?.Invoke(this, value);
            }
        }
    }

    private DisplayMode _displayMode = DisplayMode.Mini;

    public bool HasAdapters
    {
        get => _hasAdapters;
        private set => SetProperty(ref _hasAdapters, value);
    }

    public Visibility EmptyStateVisibility
    {
        get => _emptyStateVisibility;
        private set => SetProperty(ref _emptyStateVisibility, value);
    }

    public Visibility MiniModeVisibility
    {
        get => _miniModeVisibility;
        private set => SetProperty(ref _miniModeVisibility, value);
    }

    public Visibility DetailModeVisibility
    {
        get => _detailModeVisibility;
        private set => SetProperty(ref _detailModeVisibility, value);
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set
        {
            if (SetProperty(ref _isSettingsOpen, value))
            {
                ApplyViewVisibility();
            }
        }
    }

    public Visibility SettingsVisibility
    {
        get => _settingsVisibility;
        private set => SetProperty(ref _settingsVisibility, value);
    }

    public bool IsControllerTestOpen
    {
        get => _isControllerTestOpen;
        set
        {
            if (SetProperty(ref _isControllerTestOpen, value))
            {
                ApplyViewVisibility();
            }
        }
    }

    public Visibility ControllerTestVisibility
    {
        get => _controllerTestVisibility;
        private set => SetProperty(ref _controllerTestVisibility, value);
    }

    public event EventHandler<int>? AdapterCountChanged;

    public event EventHandler<DisplayMode>? DisplayModeChanged;

    public event EventHandler<WindowAnchor>? WindowAnchorChanged;

    public event EventHandler? MinimizeToTrayRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ControllerTestRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler<bool>? AlwaysOnTopChanged;

    public void ClearStartingUpState()
    {
        IsStartingUp = false;
    }

    public async Task InitializeAsync()
    {
        try
        {
            IsAlwaysOnTop = SettingsHelper.IsAlwaysOnTop;
            DisplayMode = SettingsHelper.DisplayMode;

            _runAtStartup = SettingsHelper.RunAtStartup;
            OnPropertyChanged(nameof(RunAtStartup));

            _startInTrayOnly = SettingsHelper.StartInTrayOnly;
            OnPropertyChanged(nameof(StartInTrayOnly));

            _showVirtualAdapters = SettingsHelper.ShowVirtualAdapters;
            OnPropertyChanged(nameof(ShowVirtualAdapters));

            _startupDelaySeconds = SettingsHelper.StartupDelaySeconds;
            OnPropertyChanged(nameof(StartupDelaySeconds));

            _selectedWindowAnchor = SettingsHelper.WindowAnchor;
            OnPropertyChanged(nameof(SelectedWindowAnchor));

            _enableEfficiencyMode = SettingsHelper.EnableEfficiencyMode;
            OnPropertyChanged(nameof(EnableEfficiencyMode));

            _miniRotateAdapters = SettingsHelper.MiniRotateAdapters;
            OnPropertyChanged(nameof(MiniRotateAdapters));

            StartupHelper.SyncWithSetting(_runAtStartup);

            var delaySeconds = SettingsHelper.StartupDelaySeconds;
            if (delaySeconds > 0)
            {
                IsStartingUp = true;
                await WaitForStartupDelayAsync(delaySeconds).ConfigureAwait(true);
            }

            await RefreshAsync().ConfigureAwait(true);
            _networkMonitor.StartMonitoring();
            StartBatteryMonitoring();
            _ = CheckForUpdatesCoreAsync(automatic: true);
        }
        finally
        {
            IsStartingUp = false;
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            var snapshot = await _networkMonitor.GetSnapshotAsync().ConfigureAwait(false);
            await RunOnUiThreadAsync(() => ApplySnapshot(snapshot, forceUpdate: true)).ConfigureAwait(true);
            await RefreshBatteryAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"RefreshAsync failed: {ex}");
        }
    }

    [RelayCommand]
    private void ToggleDisplayMode()
    {
        DisplayMode = DisplayMode == DisplayMode.Mini ? DisplayMode.Detail : DisplayMode.Mini;
        SettingsHelper.DisplayMode = DisplayMode;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenControllerTest()
    {
        ControllerTestRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CopyPrimaryIp()
    {
        CopyIpToClipboard(MiniDisplayedAdapter.IpAddressDisplay);
    }

    [RelayCommand]
    private void MinimizeToTray()
    {
        MinimizeToTrayRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Exit()
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private Task CheckForUpdatesAsync() => CheckForUpdatesCoreAsync(automatic: false);

    private async Task CheckForUpdatesCoreAsync(bool automatic)
    {
        if (automatic)
        {
            var lastCheck = SettingsHelper.LastUpdateCheckUtc;
            if (lastCheck.HasValue && DateTime.UtcNow - lastCheck.Value < TimeSpan.FromHours(24))
            {
                return;
            }
        }

        if (IsCheckingForUpdates)
        {
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            IsCheckingForUpdates = true;
            if (!automatic)
            {
                UpdateStatusText = "確認中...";
            }
        }).ConfigureAwait(true);

        try
        {
            var update = await _updateCheckService.CheckLatestAsync().ConfigureAwait(false);
            SettingsHelper.LastUpdateCheckUtc = DateTime.UtcNow;

            await RunOnUiThreadAsync(() =>
            {
                if (update is null)
                {
                    UpdateStatusText = "確認できませんでした";
                    ApplyUpdateAvailability(false, null);
                    return;
                }

                ApplyUpdateAvailability(update.IsNewerThanCurrent, update.PreferredDownloadUrl);
                UpdateStatusText = update.IsNewerThanCurrent
                    ? $"v{update.LatestVersion} が利用可能です（現在 {AppVersionText}）"
                    : $"最新です（{AppVersionText}）";
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"CheckForUpdatesAsync failed: {ex}");
            await RunOnUiThreadAsync(() =>
            {
                UpdateStatusText = "確認できませんでした";
                ApplyUpdateAvailability(false, null);
            }).ConfigureAwait(true);
        }
        finally
        {
            await RunOnUiThreadAsync(() => IsCheckingForUpdates = false).ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenUpdateDownload))]
    private async Task OpenUpdateDownloadAsync()
    {
        if (string.IsNullOrWhiteSpace(_updateDownloadUrl))
        {
            return;
        }

        if (!UpdateInstallerHelper.IsInstallerUrl(_updateDownloadUrl))
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(_updateDownloadUrl));
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            IsInstallingUpdate = true;
            UpdateStatusText = "インストーラーを取得中...";
        }).ConfigureAwait(true);

        try
        {
            var installerPath = await UpdateInstallerHelper
                .DownloadInstallerAsync(_updateDownloadUrl)
                .ConfigureAwait(false);

            await RunOnUiThreadAsync(() =>
            {
                UpdateStatusText = "インストーラーを起動しています...";
            }).ConfigureAwait(true);

            UpdateInstallerHelper.LaunchInstaller(installerPath);
            App.Shutdown();
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"OpenUpdateDownloadAsync failed: {ex}");
            await RunOnUiThreadAsync(() =>
            {
                UpdateStatusText = "インストールを開始できませんでした";
            }).ConfigureAwait(true);
        }
        finally
        {
            await RunOnUiThreadAsync(() => IsInstallingUpdate = false).ConfigureAwait(true);
        }
    }

    private bool CanOpenUpdateDownload() =>
        _isUpdateAvailable
        && !string.IsNullOrWhiteSpace(_updateDownloadUrl)
        && !IsInstallingUpdate;

    private void ApplyUpdateAvailability(bool isAvailable, string? downloadUrl)
    {
        _isUpdateAvailable = isAvailable;
        _updateDownloadUrl = downloadUrl;
        OnPropertyChanged(nameof(UpdateDownloadVisibility));
        OpenUpdateDownloadCommand.NotifyCanExecuteChanged();
    }

    private void OnSnapshotChanged(object? sender, NetworkSnapshot snapshot)
    {
        _ = RunOnUiThreadAsync(() => ApplySnapshot(snapshot, forceUpdate: false))
            .ContinueWith(static task =>
            {
                if (task.IsFaulted && task.Exception is not null)
                {
                    App.WriteStartupLog($"ApplySnapshot failed: {task.Exception.GetBaseException()}");
                }
            }, TaskScheduler.Default);
    }

    private void ApplySnapshot(NetworkSnapshot snapshot, bool forceUpdate)
    {
        if (!App.DispatcherQueue.HasThreadAccess)
        {
            _ = RunOnUiThreadAsync(() => ApplySnapshot(snapshot, forceUpdate));
            return;
        }

        try
        {
            if (!forceUpdate && snapshot.Fingerprint == _lastFingerprint)
            {
                return;
            }

            _lastFingerprint = snapshot.Fingerprint;
            SyncAdapters(snapshot.Adapters);

            if (snapshot.PrimaryAdapter is not null)
            {
                _primaryAdapter.UpdateFrom(snapshot.PrimaryAdapter);
                OnPropertyChanged(nameof(PrimaryAdapter));
            }

            RebuildMiniRotationSlots();
            ApplyMiniDisplayedAdapter();
            SyncMiniRotationTimer();

            HasAdapters = Adapters.Count > 0;
            EmptyStateVisibility = HasAdapters ? Visibility.Collapsed : Visibility.Visible;
            LastUpdatedText = $"更新: {DateTime.Now:HH:mm:ss}";
            UsbLanSummaryText = snapshot.UsbLan.Summary;
            UsbLanDetailText = snapshot.UsbLan.Detail;
            OnPropertyChanged(nameof(UsbLanStatusLine));
            OnPropertyChanged(nameof(UsbLanDetailVisibility));
            TrayTooltipText = BuildTrayTooltip(snapshot);

            var trayState = snapshot.PrimaryAdapter is { } primary
                ? TrayIconHelper.FromAssignmentMode(primary.AssignmentMode)
                : TrayIconState.NoIp;
            TrayIconState = trayState;
            OnPropertyChanged(nameof(TrayIconStateLabel));

            AdapterCountChanged?.Invoke(this, Adapters.Count);
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"ApplySnapshot failed: {ex}");
        }
    }

    private void SyncAdapters(IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        var incomingNames = adapters.Select(a => a.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = Adapters.Count - 1; i >= 0; i--)
        {
            if (!incomingNames.Contains(Adapters[i].Name))
            {
                _adapterLookup.Remove(Adapters[i].Name);
                Adapters.RemoveAt(i);
            }
        }

        foreach (var adapter in adapters)
        {
            if (_adapterLookup.TryGetValue(adapter.Name, out var existing))
            {
                if (!existing.Matches(adapter))
                {
                    existing.UpdateFrom(adapter);
                }
            }
            else
            {
                var item = new NetworkAdapterItemViewModel(adapter);
                _adapterLookup[adapter.Name] = item;
                Adapters.Add(item);
            }
        }
    }

    private void ApplyViewVisibility()
    {
        if (IsSettingsOpen)
        {
            MiniModeVisibility = Visibility.Collapsed;
            DetailModeVisibility = Visibility.Collapsed;
            ControllerTestVisibility = Visibility.Collapsed;
            SettingsVisibility = Visibility.Visible;
            return;
        }

        if (IsControllerTestOpen)
        {
            MiniModeVisibility = Visibility.Collapsed;
            DetailModeVisibility = Visibility.Collapsed;
            SettingsVisibility = Visibility.Collapsed;
            ControllerTestVisibility = Visibility.Visible;
            return;
        }

        SettingsVisibility = Visibility.Collapsed;
        ControllerTestVisibility = Visibility.Collapsed;
        MiniModeVisibility = DisplayMode == DisplayMode.Mini ? Visibility.Visible : Visibility.Collapsed;
        DetailModeVisibility = DisplayMode == DisplayMode.Detail ? Visibility.Visible : Visibility.Collapsed;
        SyncMiniRotationTimer();
        OnPropertyChanged(nameof(MiniRotationIndicatorVisibility));
    }

    private void RebuildMiniRotationSlots()
    {
        var previousCount = _miniRotationSlots.Count;
        _miniRotationSlots.Clear();

        var usbLan = Adapters.FirstOrDefault(IsUsbLanRotationCandidate);
        if (usbLan is not null)
        {
            _miniRotationSlots.Add(usbLan);
        }

        var wifi = Adapters.FirstOrDefault(IsWifiRotationCandidate);
        if (wifi is not null)
        {
            _miniRotationSlots.Add(wifi);
        }

        if (previousCount != _miniRotationSlots.Count || _miniRotationIndex >= _miniRotationSlots.Count)
        {
            _miniRotationIndex = 0;
        }
    }

    private static bool IsUsbLanRotationCandidate(NetworkAdapterItemViewModel adapter) =>
        adapter.IsUsbLan
        && (adapter.IpAddressDisplay != "—" || adapter.LinkStatusVisibility == Visibility.Visible);

    private static bool IsWifiRotationCandidate(NetworkAdapterItemViewModel adapter) =>
        !adapter.IsUsbLan && IsWifiAdapterName(adapter.Name);

    private static bool IsWifiAdapterName(string name) =>
        name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Wireless", StringComparison.OrdinalIgnoreCase)
        || name.Contains("WLAN", StringComparison.OrdinalIgnoreCase);

    private void ApplyMiniDisplayedAdapter()
    {
        if (!MiniRotateAdapters || _miniRotationSlots.Count <= 1)
        {
            if (_miniRotationSlots.Count == 1)
            {
                _miniDisplayedAdapter.CopyDisplayFrom(_miniRotationSlots[0]);
            }
            else
            {
                _miniDisplayedAdapter.CopyDisplayFrom(_primaryAdapter);
            }
        }
        else
        {
            _miniDisplayedAdapter.CopyDisplayFrom(_miniRotationSlots[_miniRotationIndex]);
        }

        OnPropertyChanged(nameof(MiniDisplayedAdapter));
        OnPropertyChanged(nameof(MiniRotationIndicatorText));
        OnPropertyChanged(nameof(MiniRotationIndicatorVisibility));
    }

    private void SyncMiniRotationTimer()
    {
        var shouldRotate = MiniRotateAdapters
            && _miniRotationSlots.Count > 1
            && DisplayMode == DisplayMode.Mini
            && !IsSettingsOpen
            && !IsControllerTestOpen;

        if (!shouldRotate)
        {
            StopMiniRotationTimer();
            return;
        }

        if (_miniRotationTimer is null)
        {
            _miniRotationTimer = App.DispatcherQueue.CreateTimer();
            _miniRotationTimer.Interval = TimeSpan.FromSeconds(SettingsHelper.MiniRotateIntervalSeconds);
            _miniRotationTimer.Tick += OnMiniRotationTimerTick;
        }
        else
        {
            _miniRotationTimer.Interval = TimeSpan.FromSeconds(SettingsHelper.MiniRotateIntervalSeconds);
        }

        if (!_miniRotationTimer.IsRunning)
        {
            _miniRotationTimer.Start();
        }
    }

    private void StopMiniRotationTimer()
    {
        if (_miniRotationTimer?.IsRunning == true)
        {
            _miniRotationTimer.Stop();
        }
    }

    private void OnMiniRotationTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (_miniRotationSlots.Count <= 1)
        {
            StopMiniRotationTimer();
            return;
        }

        _miniRotationIndex = (_miniRotationIndex + 1) % _miniRotationSlots.Count;
        ApplyMiniDisplayedAdapter();
    }

    private static string BuildTrayTooltip(NetworkSnapshot snapshot)
    {
        var usbLine = snapshot.UsbLan.IsDetected
            ? $"{snapshot.UsbLan.Summary}\n{snapshot.UsbLan.Detail}"
            : snapshot.UsbLan.Summary;

        if (snapshot.PrimaryAdapter is { } primary)
        {
            var mode = primary.AssignmentMode switch
            {
                IpAssignmentMode.Dhcp => "自動",
                IpAssignmentMode.Static => "手動",
                _ => "未取得"
            };
            var ip = string.IsNullOrWhiteSpace(primary.IPv4Address) ? "—" : primary.IPv4Address;
            var shortName = ShortenAdapterName(primary.Name);
            return $"IP Checker\n{usbLine}\n{shortName}: {ip} ({mode})";
        }

        return $"IP Checker\n{usbLine}";
    }

    private static string ShortenAdapterName(string name)
    {
        if (name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Wireless", StringComparison.OrdinalIgnoreCase))
        {
            return "Wi-Fi";
        }

        if (name.Contains("Ethernet", StringComparison.OrdinalIgnoreCase)
            || name.Contains("イーサネット", StringComparison.OrdinalIgnoreCase))
        {
            return "有線";
        }

        return name.Length > 20 ? name[..20] + "…" : name;
    }

    private static void CopyIpToClipboard(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || ipAddress == "—")
        {
            return;
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(ipAddress);
        Clipboard.SetContent(dataPackage);
    }

    private void StartBatteryMonitoring()
    {
        _batteryTimer?.Stop();
        _batteryTimer = App.DispatcherQueue.CreateTimer();
        _batteryTimer.Interval = TimeSpan.FromSeconds(5);
        _batteryTimer.Tick += OnBatteryTimerTick;
        _batteryTimer.Start();
        _ = RefreshBatteryAsync();
    }

    private void OnBatteryTimerTick(DispatcherQueueTimer sender, object args) =>
        _ = RefreshBatteryAsync();

    private static Task WaitForStartupDelayAsync(int delaySeconds)
    {
        if (delaySeconds <= 0)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = App.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(delaySeconds);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            tcs.TrySetResult();
        };
        timer.Start();
        return tcs.Task;
    }

    public async Task RefreshBatteryAsync()
    {
        try
        {
            var status = await BatteryStatusHelper.TryGetStatusAsync().ConfigureAwait(false);
            await RunOnUiThreadAsync(() =>
            {
                _batteryPercent = status?.Percent;
                _batteryPowerState = status?.PowerState ?? BatteryPowerState.Discharging;
                OnPropertyChanged(nameof(BatteryPercentText));
                OnPropertyChanged(nameof(BatteryIconGlyph));
                OnPropertyChanged(nameof(BatteryIconForeground));
                OnPropertyChanged(nameof(BatteryPercentForeground));
                OnPropertyChanged(nameof(BatteryPercentFontWeight));
                OnPropertyChanged(nameof(BatteryTooltipText));
                OnPropertyChanged(nameof(BatteryVisibility));
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"RefreshBatteryAsync failed: {ex}");
        }
    }

    private static Brush GetThemeBrush(string key, Windows.UI.Color fallbackColor)
    {
        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallbackColor);
    }

    private static Task RunOnUiThreadAsync(Action action)
    {
        if (App.DispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        App.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}
