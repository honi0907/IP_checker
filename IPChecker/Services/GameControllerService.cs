using IPChecker.Models;
using Microsoft.UI.Dispatching;
using Windows.Gaming.Input;

namespace IPChecker.Services;

public sealed class GameControllerService : IGameControllerService
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _timer;
    private RawGameController? _controller;
    private ControllerInputSnapshot _currentSnapshot = ControllerInputSnapshot.Empty;
    private bool[] _buttons = [];
    private GameControllerSwitchPosition[] _switches = [];
    private double[] _axes = [];
    private bool _isPolling;

    public GameControllerService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        _timer = dispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(30);
        _timer.Tick += OnTimerTick;
    }

    public event EventHandler<ControllerInputSnapshot>? SnapshotChanged;

    public ControllerInputSnapshot CurrentSnapshot => _currentSnapshot;

    public void StartPolling()
    {
        if (_isPolling)
        {
            return;
        }

        _isPolling = true;
        RawGameController.RawGameControllerAdded += OnControllerAdded;
        RawGameController.RawGameControllerRemoved += OnControllerRemoved;
        AttachFirstController();
        _timer.Start();
        Poll();
    }

    public void StopPolling()
    {
        if (!_isPolling)
        {
            return;
        }

        _isPolling = false;
        _timer.Stop();
        _controller = null;
        RawGameController.RawGameControllerAdded -= OnControllerAdded;
        RawGameController.RawGameControllerRemoved -= OnControllerRemoved;
        UpdateSnapshot(ControllerInputSnapshot.Empty);
    }

    public void Dispose()
    {
        StopPolling();
        _timer.Tick -= OnTimerTick;
    }

    private void OnControllerAdded(object? sender, RawGameController e)
    {
        if (!_isPolling || _controller is not null)
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(AttachFirstController);
    }

    private void OnControllerRemoved(object? sender, RawGameController e)
    {
        if (!ReferenceEquals(_controller, e))
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            _controller = null;
            if (_isPolling)
            {
                AttachFirstController();
                Poll();
            }
        });
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        Poll();
    }

    private void AttachFirstController()
    {
        _controller = RawGameController.RawGameControllers.FirstOrDefault();
        if (_controller is null)
        {
            _buttons = [];
            _switches = [];
            _axes = [];
            UpdateSnapshot(ControllerInputSnapshot.Empty);
            return;
        }

        _buttons = new bool[_controller.ButtonCount];
        _switches = new GameControllerSwitchPosition[_controller.SwitchCount];
        _axes = new double[_controller.AxisCount];
    }

    private void Poll()
    {
        if (_controller is null)
        {
            AttachFirstController();
            if (_controller is null)
            {
                return;
            }
        }

        try
        {
            _controller.GetCurrentReading(_buttons, _switches, _axes);

            var deviceName = string.IsNullOrWhiteSpace(_controller.DisplayName)
                ? "ゲームコントローラ"
                : _controller.DisplayName;

            var stickX = _axes.Length > 0 ? NormalizeAxis(_axes[0]) : 0;
            var stickY = _axes.Length > 1 ? NormalizeAxis(_axes[1]) : 0;
            var hasThrottle = _axes.Length > 2;
            var throttle = hasThrottle ? _axes[2] : 0;

            var hat = _switches.Length > 0
                ? _switches[0]
                : GameControllerSwitchPosition.Center;

            UpdateSnapshot(new ControllerInputSnapshot
            {
                IsConnected = true,
                DeviceName = deviceName,
                Buttons = _buttons.ToArray(),
                Axes = _axes.ToArray(),
                HatSwitch = hat,
                StickX = stickX,
                StickY = stickY,
                Throttle = throttle,
                HasThrottleAxis = hasThrottle
            });
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"Controller poll failed: {ex}");
            _controller = null;
            UpdateSnapshot(ControllerInputSnapshot.Empty);
        }
    }

    private void UpdateSnapshot(ControllerInputSnapshot snapshot)
    {
        _currentSnapshot = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
    }

    private static double NormalizeAxis(double value)
    {
        return Math.Clamp((value * 2.0) - 1.0, -1.0, 1.0);
    }
}
