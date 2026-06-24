using IPChecker.Models;

namespace IPChecker.Services;

public interface IUpdateCheckService
{
    Task<AppUpdateInfo?> CheckLatestAsync(CancellationToken cancellationToken = default);
}
