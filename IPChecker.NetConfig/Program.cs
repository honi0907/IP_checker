using System.Text.Json;
using System.Text.Json.Serialization;

namespace IPChecker.NetConfig;

internal sealed class NetworkConfigRequest
{
    public uint ConfigurationIndex { get; init; }

    public required string ExpectedAdapterName { get; init; }

    public required string Mode { get; init; }

    public string? IPv4Address { get; init; }

    public string? SubnetMask { get; init; }

    public string? DefaultGateway { get; init; }
}

internal sealed class NetworkConfigResult
{
    public bool Success { get; init; }

    public int ReturnCode { get; init; }

    public required string Message { get; init; }
}

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            WriteResult(args.Length > 1 ? args[1] : null, false, -1, "引数が不足しています。");
            return 1;
        }

        var requestPath = args[0];
        var resultPath = args[1];

        try
        {
            var json = File.ReadAllText(requestPath);
            var request = JsonSerializer.Deserialize<NetworkConfigRequest>(json, JsonOptions);
            if (request is null)
            {
                WriteResult(resultPath, false, -1, "要求ファイルの読み取りに失敗しました。");
                return 1;
            }

            var result = WmiNetworkConfigurator.Apply(request);
            WriteResult(resultPath, result.Success, result.ReturnCode, result.Message);
            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            WriteResult(resultPath, false, -1, ex.Message);
            return 1;
        }
    }

    private static void WriteResult(string? resultPath, bool success, int returnCode, string message)
    {
        var result = new NetworkConfigResult
        {
            Success = success,
            ReturnCode = returnCode,
            Message = message
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        if (!string.IsNullOrWhiteSpace(resultPath))
        {
            File.WriteAllText(resultPath, json);
        }

        Console.WriteLine(json);
    }
}
