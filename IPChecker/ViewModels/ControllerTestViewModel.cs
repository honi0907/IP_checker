using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IPChecker.Models;
using IPChecker.Services;
using Microsoft.UI.Xaml;
using Windows.Gaming.Input;

namespace IPChecker.ViewModels;

public partial class ControllerTestViewModel : ObservableObject
{
    private const int MinimumButtonSlots = 8;

    private readonly IGameControllerService _gameControllerService;
    private string _deviceName = "コントローラが見つかりません";
    private bool _isConnected;
    private double _stickX;
    private double _stickY;
    private double _throttle;
    private bool _hasThrottleAxis;
    private Visibility _throttleVisibility = Visibility.Collapsed;
    private Visibility _notConnectedVisibility = Visibility.Visible;
    private Visibility _connectedVisibility = Visibility.Collapsed;
    private double _stickDotLeft = 48;
    private double _stickDotTop = 48;
    private GameControllerSwitchPosition _hatSwitch = GameControllerSwitchPosition.Center;

    public ControllerTestViewModel(IGameControllerService gameControllerService)
    {
        _gameControllerService = gameControllerService;
        _gameControllerService.SnapshotChanged += OnSnapshotChanged;
        ApplySnapshot(_gameControllerService.CurrentSnapshot);
    }

    public ObservableCollection<ControllerButtonItemViewModel> Buttons { get; } = [];

    public string DeviceName
    {
        get => _deviceName;
        private set => SetProperty(ref _deviceName, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    public double StickX
    {
        get => _stickX;
        private set => SetProperty(ref _stickX, value);
    }

    public double StickY
    {
        get => _stickY;
        private set => SetProperty(ref _stickY, value);
    }

    public double Throttle
    {
        get => _throttle;
        private set => SetProperty(ref _throttle, value);
    }

    public bool HasThrottleAxis
    {
        get => _hasThrottleAxis;
        private set => SetProperty(ref _hasThrottleAxis, value);
    }

    public Visibility ThrottleVisibility
    {
        get => _throttleVisibility;
        private set => SetProperty(ref _throttleVisibility, value);
    }

    public Visibility NotConnectedVisibility
    {
        get => _notConnectedVisibility;
        private set => SetProperty(ref _notConnectedVisibility, value);
    }

    public Visibility ConnectedVisibility
    {
        get => _connectedVisibility;
        private set => SetProperty(ref _connectedVisibility, value);
    }

    public double StickDotLeft
    {
        get => _stickDotLeft;
        private set => SetProperty(ref _stickDotLeft, value);
    }

    public double StickDotTop
    {
        get => _stickDotTop;
        private set => SetProperty(ref _stickDotTop, value);
    }

    public GameControllerSwitchPosition HatSwitch
    {
        get => _hatSwitch;
        private set
        {
            if (SetProperty(ref _hatSwitch, value))
            {
                OnPropertyChanged(nameof(HatUpVisibility));
                OnPropertyChanged(nameof(HatRightVisibility));
                OnPropertyChanged(nameof(HatDownVisibility));
                OnPropertyChanged(nameof(HatLeftVisibility));
            }
        }
    }

    public Visibility HatUpVisibility => GetHatVisibility(
        GameControllerSwitchPosition.Up,
        GameControllerSwitchPosition.UpRight,
        GameControllerSwitchPosition.UpLeft);

    public Visibility HatRightVisibility => GetHatVisibility(
        GameControllerSwitchPosition.Right,
        GameControllerSwitchPosition.UpRight,
        GameControllerSwitchPosition.DownRight);

    public Visibility HatDownVisibility => GetHatVisibility(
        GameControllerSwitchPosition.Down,
        GameControllerSwitchPosition.DownRight,
        GameControllerSwitchPosition.DownLeft);

    public Visibility HatLeftVisibility => GetHatVisibility(
        GameControllerSwitchPosition.Left,
        GameControllerSwitchPosition.UpLeft,
        GameControllerSwitchPosition.DownLeft);

    public void StartMonitoring()
    {
        _gameControllerService.StartPolling();
    }

    public void StopMonitoring()
    {
        _gameControllerService.StopPolling();
    }

    private void OnSnapshotChanged(object? sender, ControllerInputSnapshot snapshot)
    {
        if (!App.DispatcherQueue.HasThreadAccess)
        {
            App.DispatcherQueue.TryEnqueue(() => ApplySnapshot(snapshot));
            return;
        }

        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(ControllerInputSnapshot snapshot)
    {
        IsConnected = snapshot.IsConnected;
        DeviceName = snapshot.IsConnected
            ? snapshot.DeviceName
            : "コントローラが見つかりません";
        NotConnectedVisibility = snapshot.IsConnected ? Visibility.Collapsed : Visibility.Visible;
        ConnectedVisibility = snapshot.IsConnected ? Visibility.Visible : Visibility.Collapsed;

        if (!snapshot.IsConnected)
        {
            Buttons.Clear();
            StickX = 0;
            StickY = 0;
            StickDotLeft = 48;
            StickDotTop = 48;
            Throttle = 0;
            HasThrottleAxis = false;
            ThrottleVisibility = Visibility.Collapsed;
            HatSwitch = GameControllerSwitchPosition.Center;
            return;
        }

        var displayCount = Math.Max(snapshot.Buttons.Count, MinimumButtonSlots);
        EnsureButtons(displayCount);
        for (var i = 0; i < Buttons.Count; i++)
        {
            var isAvailable = i < snapshot.Buttons.Count;
            Buttons[i].IsAvailable = isAvailable;
            Buttons[i].IsPressed = isAvailable && snapshot.Buttons[i];
        }

        StickX = snapshot.StickX;
        StickY = snapshot.StickY;
        StickDotLeft = ((snapshot.StickX + 1.0) * 0.5) * 96;
        StickDotTop = ((snapshot.StickY + 1.0) * 0.5) * 96;
        Throttle = snapshot.Throttle;
        HasThrottleAxis = snapshot.HasThrottleAxis;
        ThrottleVisibility = snapshot.HasThrottleAxis ? Visibility.Visible : Visibility.Collapsed;
        HatSwitch = snapshot.HatSwitch;
    }

    private void EnsureButtons(int count)
    {
        while (Buttons.Count < count)
        {
            Buttons.Add(new ControllerButtonItemViewModel
            {
                Number = Buttons.Count + 1
            });
        }

        while (Buttons.Count > count)
        {
            Buttons.RemoveAt(Buttons.Count - 1);
        }
    }

    private Visibility GetHatVisibility(params GameControllerSwitchPosition[] activePositions)
    {
        return activePositions.Contains(HatSwitch) ? Visibility.Visible : Visibility.Collapsed;
    }
}
