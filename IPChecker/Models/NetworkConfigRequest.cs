namespace IPChecker.Models;

public sealed class NetworkConfigRequest
{
    public uint ConfigurationIndex { get; init; }

    public required string ExpectedAdapterName { get; init; }

    public required string Mode { get; init; }

    public string? IPv4Address { get; init; }

    public string? SubnetMask { get; init; }

    public string? DefaultGateway { get; init; }
}

public sealed class NetworkConfigResult
{
    public bool Success { get; init; }

    public int ReturnCode { get; init; }

    public required string Message { get; init; }
}
