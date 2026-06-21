using Windows.Gaming.Input;

namespace IPChecker.Models;

public sealed class ControllerInputSnapshot
{
    public static ControllerInputSnapshot Empty { get; } = new();

    public bool IsConnected { get; init; }

    public string DeviceName { get; init; } = string.Empty;

    public IReadOnlyList<bool> Buttons { get; init; } = [];

    public IReadOnlyList<double> Axes { get; init; } = [];

    public GameControllerSwitchPosition HatSwitch { get; init; } = GameControllerSwitchPosition.Center;

    public double StickX { get; init; }

    public double StickY { get; init; }

    public double Throttle { get; init; }

    public bool HasThrottleAxis { get; init; }
}
