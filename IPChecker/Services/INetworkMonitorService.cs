using IPChecker.Models;

namespace IPChecker.Services;

public interface INetworkMonitorService : IDisposable
{
    event EventHandler<NetworkSnapshot>? SnapshotChanged;

    Task<NetworkSnapshot> GetSnapshotAsync();

    void StartMonitoring();

    void StopMonitoring();

    void SetWindowVisible(bool isVisible);

    void ReapplyEfficiencyProfile();

    bool IsWindowVisible { get; }
}
