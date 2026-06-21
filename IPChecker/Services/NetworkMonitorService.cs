using System.Management;

using System.Net;

using System.Net.NetworkInformation;

using System.Net.Sockets;

using IPChecker.Helpers;

using IPChecker.Models;



namespace IPChecker.Services;



public sealed class NetworkMonitorService : INetworkMonitorService

{

    private const int ActivePollIntervalSeconds = 90;

    private const int BackgroundPollIntervalSeconds = 300;



    private readonly object _sync = new();

    private readonly CancellationTokenSource _cts = new();

    private Task? _pollTask;

    private bool _isMonitoring;

    private bool _isWindowVisible = true;

    private int _pollIntervalSeconds = ActivePollIntervalSeconds;

    private string _lastFingerprint = string.Empty;



    public bool IsWindowVisible

    {

        get

        {

            lock (_sync)

            {

                return _isWindowVisible;

            }

        }

    }



    public event EventHandler<NetworkSnapshot>? SnapshotChanged;



    public void StartMonitoring()

    {

        lock (_sync)

        {

            if (_isMonitoring)

            {

                return;

            }



            _isMonitoring = true;

        }



        ApplyEfficiencyProfile();



        NetworkChange.NetworkAddressChanged += OnNetworkChanged;

        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;

        _pollTask = PollLoopAsync(_cts.Token);

        _ = RefreshAsync(forceNotify: true);

    }



    public void StopMonitoring()

    {

        lock (_sync)

        {

            if (!_isMonitoring)

            {

                return;

            }



            _isMonitoring = false;

        }



        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;

        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;

        _cts.Cancel();

    }



    public void SetWindowVisible(bool isVisible)

    {

        lock (_sync)

        {

            if (_isWindowVisible == isVisible)

            {

                return;

            }



            _isWindowVisible = isVisible;

        }



        ApplyEfficiencyProfile();

    }



    public void ReapplyEfficiencyProfile()

    {

        ApplyEfficiencyProfile();

    }



    public async Task<NetworkSnapshot> GetSnapshotAsync()

    {

        return await StaTaskRunner.RunAsync(BuildSnapshot).ConfigureAwait(false);

    }



    public void Dispose()

    {

        StopMonitoring();

        _cts.Dispose();

        EfficiencyModeHelper.SetEcoMode(false);

    }



    private void OnNetworkChanged(object? sender, EventArgs e)

    {

        _ = RefreshAsync(forceNotify: false);

    }



    private async Task PollLoopAsync(CancellationToken cancellationToken)

    {

        try

        {

            while (!cancellationToken.IsCancellationRequested)

            {

                var delaySeconds = GetPollIntervalSeconds();

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);

                await RefreshAsync(forceNotify: false).ConfigureAwait(false);

            }

        }

        catch (OperationCanceledException)

        {

            // Expected on shutdown.

        }

    }



    private async Task RefreshAsync(bool forceNotify)

    {

        try

        {

            var snapshot = await GetSnapshotAsync().ConfigureAwait(false);



            if (!forceNotify && snapshot.Fingerprint == _lastFingerprint)

            {

                return;

            }



            _lastFingerprint = snapshot.Fingerprint;

            SnapshotChanged?.Invoke(this, snapshot);

        }

        catch (Exception ex)

        {

            App.WriteStartupLog($"Network refresh failed: {ex}");

        }

    }



    private int GetPollIntervalSeconds()

    {

        lock (_sync)

        {

            return _pollIntervalSeconds;

        }

    }



    private void ApplyEfficiencyProfile()

    {

        if (!SettingsHelper.EnableEfficiencyMode)

        {

            lock (_sync)

            {

                _pollIntervalSeconds = ActivePollIntervalSeconds;

            }



            EfficiencyModeHelper.SetEcoMode(false);

            return;

        }



        var useBackgroundProfile = !_isWindowVisible;



        lock (_sync)

        {

            _pollIntervalSeconds = useBackgroundProfile

                ? BackgroundPollIntervalSeconds

                : ActivePollIntervalSeconds;

        }



        EfficiencyModeHelper.SetEcoMode(useBackgroundProfile);

    }



    private NetworkSnapshot BuildSnapshot()

    {

        var rawAdapters = QueryAdapters();

        var showVirtual = SettingsHelper.ShowVirtualAdapters;

        var filtered = rawAdapters

            .Where(a => showVirtual || !a.IsVirtual)

            .ToList();



        var primary = SelectPrimaryAdapter(filtered);



        var adapters = filtered

            .Select(a => a with { IsPrimary = primary is not null && a.Name == primary.Name })

            .OrderByDescending(a => a.IsPrimary)

            .ThenByDescending(a => a.IsConnected)

            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)

            .ToList();



        var primaryInList = adapters.FirstOrDefault(a => a.IsPrimary) ?? adapters.FirstOrDefault();

        var usbLanAdapters = adapters.Where(a => a.IsUsbLan).ToList();

        return new NetworkSnapshot

        {

            Adapters = adapters,

            PrimaryAdapter = primaryInList,

            UsbLan = UsbLanDetector.BuildStatus(usbLanAdapters)

        };

    }



    private static NetworkAdapterInfo? SelectPrimaryAdapter(IReadOnlyList<NetworkAdapterInfo> adapters)

    {

        var usbLanStatic = adapters

            .FirstOrDefault(a => a.IsUsbLan && a.AssignmentMode == IpAssignmentMode.Static);

        if (usbLanStatic is not null)

        {

            return usbLanStatic;

        }



        var withGateway = adapters

            .Where(a => a.IsConnected && !string.IsNullOrWhiteSpace(a.DefaultGateway))

            .ToList();



        if (withGateway.Count == 1)

        {

            return withGateway[0];

        }



        if (withGateway.Count > 1)

        {

            return withGateway

                .OrderByDescending(a => a.Name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)

                    || a.Name.Contains("Wireless", StringComparison.OrdinalIgnoreCase))

                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)

                .First();

        }



        return adapters.FirstOrDefault(a => a.IsUsbLan && a.UsbLanState == UsbLanRecognitionState.Recognized)

            ?? adapters.FirstOrDefault(a => a.IsConnected)

            ?? adapters.FirstOrDefault();

    }



    private static List<NetworkAdapterInfo> QueryAdapters()

    {

        var configsByIndex = QueryAdapterConfigurationsByIndex();

        var results = new List<NetworkAdapterInfo>();

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);



        foreach (var config in configsByIndex.Values)

        {

            if (!seenNames.Add(config.Name))

            {

                continue;

            }



            results.Add(config);

        }



        foreach (var physical in UsbLanDetector.QueryPhysicalUsbLanAdapters())

        {

            if (configsByIndex.TryGetValue(physical.Index, out var configured))

            {

                var enriched = ApplyConfiguredIpFallback(configured, physical.Index);

                var merged = enriched with

                {

                    IsUsbLan = true,

                    UsbLanState = ResolveUsbLanState(physical, enriched),

                    LinkStatusLabel = UsbLanDetector.MapLinkStatusLabel(physical.NetConnectionStatus)

                };



                var index = results.FindIndex(a =>

                    string.Equals(a.Name, configured.Name, StringComparison.OrdinalIgnoreCase));

                if (index >= 0)

                {

                    results[index] = merged;

                }



                continue;

            }



            var configByIndex = TryGetConfigurationByIndex(physical.Index);

            if (configByIndex is not null)

            {

                if (!seenNames.Add(configByIndex.Name))

                {

                    continue;

                }



                results.Add(configByIndex with

                {

                    IsUsbLan = true,

                    UsbLanState = ResolveUsbLanState(physical, configByIndex),

                    LinkStatusLabel = UsbLanDetector.MapLinkStatusLabel(physical.NetConnectionStatus)

                });

                continue;

            }



            if (!seenNames.Add(physical.Name))

            {

                continue;

            }



            results.Add(new NetworkAdapterInfo

            {

                Name = physical.Name,

                IPv4Address = null,

                DefaultGateway = null,

                AssignmentMode = IpAssignmentMode.NoIp,

                IsConnected = false,

                IsVirtual = false,

                IsUsbLan = true,

                UsbLanState = ResolveUsbLanState(physical, null),

                LinkStatusLabel = UsbLanDetector.MapLinkStatusLabel(physical.NetConnectionStatus),

                ConfigurationIndex = physical.Index

            });

        }



        return results;

    }



    private static Dictionary<uint, NetworkAdapterInfo> QueryAdapterConfigurationsByIndex()

    {

        var results = new Dictionary<uint, NetworkAdapterInfo>();



        try

        {

            using var searcher = new ManagementObjectSearcher(

                "SELECT Index, Caption, Description, IPAddress, IPSubnet, DefaultIPGateway, DHCPEnabled, IPEnabled, SettingID, DNSServerSearchOrder " +

                "FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");



            foreach (var obj in searcher.Get().Cast<ManagementObject>())

            {

                var index = obj["Index"] is uint indexValue ? indexValue : 0;

                var parsed = ParseConfigurationObject(obj, isVirtual: null);
                if (parsed is null)
                {
                    continue;
                }

                results[index] = parsed with { ConfigurationIndex = index };

            }

        }

        catch (Exception ex)

        {

            App.WriteStartupLog($"WMI query failed: {ex}");

        }



        return results;

    }



    private static NetworkAdapterInfo? TryGetConfigurationByIndex(uint adapterIndex)

    {

        try

        {

            using var searcher = new ManagementObjectSearcher(

                "SELECT Index, Caption, Description, IPAddress, IPSubnet, DefaultIPGateway, DHCPEnabled, IPEnabled, SettingID, DNSServerSearchOrder " +

                $"FROM Win32_NetworkAdapterConfiguration WHERE Index = {adapterIndex}");



            foreach (var obj in searcher.Get().Cast<ManagementObject>())

            {

                var index = obj["Index"] is uint indexValue ? indexValue : adapterIndex;

                var parsed = ParseConfigurationObject(obj, isVirtual: false);
                if (parsed is null)
                {
                    return null;
                }

                return parsed with { ConfigurationIndex = index };

            }

        }

        catch (Exception ex)

        {

            App.WriteStartupLog($"WMI query by index failed ({adapterIndex}): {ex}");

        }



        return null;

    }



    private static NetworkAdapterInfo ApplyConfiguredIpFallback(NetworkAdapterInfo info, uint adapterIndex)

    {

        if (!string.IsNullOrWhiteSpace(info.IPv4Address)

            || info.AssignmentMode == IpAssignmentMode.Dhcp)

        {

            return info;

        }



        var refreshed = TryGetConfigurationByIndex(adapterIndex);

        return refreshed ?? info;

    }



    private static NetworkAdapterInfo? ParseConfigurationObject(ManagementObject obj, bool? isVirtual = null)

    {

        var description = obj["Description"]?.ToString()?.Trim();

        var caption = obj["Caption"]?.ToString()?.Trim();

        var name = string.IsNullOrWhiteSpace(description) ? caption : description;



        if (string.IsNullOrWhiteSpace(name) || IsLoopback(name))

        {

            return null;

        }



        var ipv4 = ExtractPrimaryIPv4(obj["IPAddress"] as string[]);

        var gateway = ExtractPrimaryIPv4(obj["DefaultIPGateway"] as string[]);

        var subnetMask = ExtractPrimaryIPv4(obj["IPSubnet"] as string[]);

        var dhcpEnabled = obj["DHCPEnabled"] is true;
        var settingId = obj["SettingID"]?.ToString();

        if (!dhcpEnabled)
        {
            var registryDhcp = StaticIpConfigReader.TryReadConfiguredDhcpEnabled(settingId);
            if (registryDhcp == true)
            {
                dhcpEnabled = true;
            }
        }

        var index = obj["Index"] is uint indexValue ? indexValue : 0u;

        (ipv4, gateway) = ApplyConfiguredIpFallback(ipv4, gateway, dhcpEnabled, settingId);

        if (string.IsNullOrWhiteSpace(subnetMask) && !dhcpEnabled)

        {

            subnetMask = StaticIpConfigReader.TryReadConfiguredSubnetMask(settingId);

        }

        var assignmentMode = DetermineAssignmentMode(ipv4, dhcpEnabled);

        var virtualAdapter = isVirtual ?? IsVirtualAdapter(name);

        var dnsServers = FormatDnsServers(obj["DNSServerSearchOrder"] as string[]);



        return new NetworkAdapterInfo

        {

            Name = name,

            IPv4Address = ipv4,

            DefaultGateway = gateway,

            AssignmentMode = assignmentMode,

            IsConnected = assignmentMode is IpAssignmentMode.Dhcp or IpAssignmentMode.Static,

            IsVirtual = virtualAdapter,

            ConfigurationIndex = index,

            SettingId = settingId,

            SubnetMask = subnetMask,

            DnsServers = dnsServers

        };

    }



    private static string? FormatDnsServers(string[]? servers)

    {

        if (servers is null || servers.Length == 0)

        {

            return null;

        }



        var ipv4Servers = servers

            .Where(s => IPAddress.TryParse(s, out var parsed)

                && parsed.AddressFamily == AddressFamily.InterNetwork)

            .ToArray();



        return ipv4Servers.Length == 0 ? null : string.Join(", ", ipv4Servers);

    }



    private static (string? IPv4, string? Gateway) ApplyConfiguredIpFallback(

        string? ipv4,

        string? gateway,

        bool dhcpEnabled,

        string? settingId)

    {

        if (!dhcpEnabled)

        {

            if (string.IsNullOrWhiteSpace(ipv4))

            {

                ipv4 = StaticIpConfigReader.TryReadConfiguredIPv4(settingId);

            }



            if (string.IsNullOrWhiteSpace(gateway))

            {

                gateway = StaticIpConfigReader.TryReadConfiguredGateway(settingId);

            }

        }



        return (ipv4, gateway);

    }



    private static UsbLanRecognitionState ResolveUsbLanState(

        UsbLanDetector.PhysicalAdapter physical,

        NetworkAdapterInfo? configuration)

    {

        if (configuration?.AssignmentMode is IpAssignmentMode.Dhcp or IpAssignmentMode.Static)

        {

            return UsbLanRecognitionState.Recognized;

        }



        return physical.NetEnabled

            ? UsbLanRecognitionState.Recognized

            : UsbLanRecognitionState.Disabled;

    }



    private static bool IsLoopback(string name) =>

        name.Contains("Loopback", StringComparison.OrdinalIgnoreCase)

        || name.Contains("Software Loopback", StringComparison.OrdinalIgnoreCase);



    private static bool IsVirtualAdapter(string name)

    {

        string[] markers =

        [

            "Virtual", "Hyper-V", "WSL", "VMware", "VirtualBox",

            "TAP-", "TAP ", "VPN", "vEthernet", "Bluetooth Network"

        ];



        return markers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase));

    }



    private static string? ExtractPrimaryIPv4(string[]? addresses)

    {

        if (addresses is null || addresses.Length == 0)

        {

            return null;

        }



        foreach (var address in addresses)

        {

            if (IPAddress.TryParse(address, out var parsed)

                && parsed.AddressFamily == AddressFamily.InterNetwork

                && !IPAddress.IsLoopback(parsed)

                && !parsed.Equals(IPAddress.Any))

            {

                return parsed.ToString();

            }

        }



        return null;

    }



    private static IpAssignmentMode DetermineAssignmentMode(string? ipv4, bool dhcpEnabled)

    {

        if (dhcpEnabled)

        {

            return IpAssignmentMode.Dhcp;

        }



        if (string.IsNullOrWhiteSpace(ipv4))

        {

            return IpAssignmentMode.NoIp;

        }



        if (ipv4.StartsWith("169.254.", StringComparison.Ordinal))

        {

            return IpAssignmentMode.NoIp;

        }



        return IpAssignmentMode.Static;

    }

}


