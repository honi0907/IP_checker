using IPChecker.Models;

namespace IPChecker.Services;

public interface INetworkConfigService
{
    Task<NetworkConfigResult> ApplyAsync(NetworkConfigRequest request, CancellationToken cancellationToken = default);
}
