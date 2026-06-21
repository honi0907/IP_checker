using IPChecker.Models;

namespace IPChecker.Services;

public sealed class NullGameControllerService : IGameControllerService
{
#pragma warning disable CS0067
    public event EventHandler<ControllerInputSnapshot>? SnapshotChanged;
#pragma warning restore CS0067

    public ControllerInputSnapshot CurrentSnapshot => ControllerInputSnapshot.Empty;

    public void StartPolling()
    {
    }

    public void StopPolling()
    {
    }

    public void Dispose()
    {
    }
}
