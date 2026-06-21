using IPChecker.Models;

namespace IPChecker.Services;

public interface IGameControllerService : IDisposable
{
    event EventHandler<ControllerInputSnapshot>? SnapshotChanged;

    ControllerInputSnapshot CurrentSnapshot { get; }

    void StartPolling();

    void StopPolling();
}
