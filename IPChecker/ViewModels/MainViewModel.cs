using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IPChecker.Helpers;
using IPChecker.Models;
using IPChecker.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace IPChecker.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INetworkMonitorService _networkMonitor;
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

    public MainViewModel(INetworkMonitorService networkMonitor, IGameControllerService gameControllerService)
    {
        _networkMonitor = networkMonitor;
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

    public async Task InitializeAsync()
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
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds)).ConfigureAwait(true);
        }

        await RefreshAsync().ConfigureAwait(true);
        _networkMonitor.StartMonitoring();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            var snapshot = await _networkMonitor.GetSnapshotAsync().ConfigureAwait(false);
            await RunOnUiThreadAsync(() => ApplySnapshot(snapshot, forceUpdate: true)).ConfigureAwait(true);
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
