using System.Diagnostics;
using System.Text.Json;
using IPChecker.Helpers;
using IPChecker.Models;

namespace IPChecker.Services;

public sealed class NetworkConfigService : INetworkConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<NetworkConfigResult> ApplyAsync(
        NetworkConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = NetworkConfigValidator.Validate(request);
        if (validationError is not null)
        {
            return new NetworkConfigResult
            {
                Success = false,
                ReturnCode = -1,
                Message = validationError
            };
        }

        var helperPath = Path.Combine(AppContext.BaseDirectory, "NetConfig", "IPChecker.NetConfig.exe");
        if (!File.Exists(helperPath))
        {
            return new NetworkConfigResult
            {
                Success = false,
                ReturnCode = -1,
                Message = "IPChecker.NetConfig.exe が見つかりません。再インストールしてください。"
            };
        }

        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "IPChecker",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var requestPath = Path.Combine(tempDirectory, "request.json");
        var resultPath = Path.Combine(tempDirectory, "result.json");

        try
        {
            await File.WriteAllTextAsync(
                requestPath,
                JsonSerializer.Serialize(request, JsonOptions),
                cancellationToken).ConfigureAwait(false);

            var startInfo = new ProcessStartInfo
            {
                FileName = helperPath,
                Arguments = $"\"{requestPath}\" \"{resultPath}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new NetworkConfigResult
                {
                    Success = false,
                    ReturnCode = -1,
                    Message = "管理者権限がキャンセルされたか、設定ツールを起動できませんでした。"
                };
            }

            // runas 経由では Process がシェルラッパーを指し、WaitForExit が早く返ることがある。
            var completed = await WaitForResultFileAsync(
                resultPath,
                TimeSpan.FromMinutes(2),
                cancellationToken).ConfigureAwait(false);

            if (!completed)
            {
                return new NetworkConfigResult
                {
                    Success = false,
                    ReturnCode = process.HasExited ? process.ExitCode : -1,
                    Message = "設定結果を取得できませんでした（タイムアウトまたはキャンセル）。"
                };
            }

            var resultJson = await File.ReadAllTextAsync(resultPath, cancellationToken).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<NetworkConfigResult>(resultJson, JsonOptions)
                ?? new NetworkConfigResult
                {
                    Success = false,
                    ReturnCode = -1,
                    Message = "設定結果の解析に失敗しました。"
                };

            App.WriteStartupLog(
                $"NetworkConfig applied: success={result.Success}, code={result.ReturnCode}, message={result.Message}");

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"NetworkConfigService failed: {ex}");
            return new NetworkConfigResult
            {
                Success = false,
                ReturnCode = -1,
                Message = $"設定の適用に失敗しました: {ex.Message}"
            };
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }

    private static async Task<bool> WaitForResultFileAsync(
        string resultPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(resultPath))
            {
                try
                {
                    var info = new FileInfo(resultPath);
                    if (info.Length > 0)
                    {
                        // 書き込み完了を待つ
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                        return true;
                    }
                }
                catch (IOException)
                {
                    // まだ書き込み中の可能性がある
                }
            }

            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }
}
