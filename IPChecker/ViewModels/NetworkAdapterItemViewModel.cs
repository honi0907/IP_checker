using CommunityToolkit.Mvvm.ComponentModel;
using IPChecker.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace IPChecker.ViewModels;

public partial class NetworkAdapterItemViewModel : ObservableObject
{
    private string _name = string.Empty;
    private string _ipAddressDisplay = string.Empty;
    private string _assignmentLabel = string.Empty;
    private string _adapterIconGlyph = "\uE839";
    private Brush _badgeBackground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
    private Brush _badgeForeground = new SolidColorBrush(Microsoft.UI.Colors.White);

    public bool IsStaticAssignment => _assignmentMode == IpAssignmentMode.Static;

    public bool IsPrimary { get; private set; }

    public bool IsUsbLan { get; private set; }

    public bool IsVirtual { get; private set; }

    public uint ConfigurationIndex { get; private set; }

    public string? SubnetMask { get; private set; }

    public string? DefaultGateway { get; private set; }

    public string? DnsServers { get; private set; }

    public bool CanOpenWindowsNetwork => !IsVirtual && ConfigurationIndex > 0;

    public Visibility CanOpenWindowsNetworkVisibility =>
        CanOpenWindowsNetwork ? Visibility.Visible : Visibility.Collapsed;

    public IpAssignmentMode AssignmentMode => _assignmentMode;

    public string LinkStatusLabel
    {
        get => _linkStatusLabel;
        private set
        {
            if (SetProperty(ref _linkStatusLabel, value))
            {
                OnPropertyChanged(nameof(LinkStatusVisibility));
                NotifyMiniLayoutVisibilityChanged();
            }
        }
    }

    public Visibility LinkStatusVisibility =>
        string.IsNullOrWhiteSpace(_linkStatusLabel) ? Visibility.Collapsed : Visibility.Visible;

    public bool UseMiniUsbLanLinkStack =>
        IsUsbLan && LinkStatusVisibility == Visibility.Visible;

    public Visibility MiniUsbLanLinkStackVisibility =>
        UseMiniUsbLanLinkStack ? Visibility.Visible : Visibility.Collapsed;

    public Visibility MiniInlineShortNameVisibility =>
        UseMiniUsbLanLinkStack ? Visibility.Collapsed : Visibility.Visible;

    public Visibility MiniInlineLinkStatusVisibility =>
        !UseMiniUsbLanLinkStack && LinkStatusVisibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;

    private string _linkStatusLabel = string.Empty;

    private IpAssignmentMode _assignmentMode = IpAssignmentMode.NoIp;

    public Brush RowBackground { get; private set; } =
        new SolidColorBrush(Windows.UI.Color.FromArgb(255, 44, 44, 44));

    public NetworkAdapterItemViewModel(NetworkAdapterInfo info)
    {
        UpdateFrom(info);
    }

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    public string ShortName
    {
        get => _shortName;
        private set => SetProperty(ref _shortName, value);
    }

    public string AssignmentShortLabel
    {
        get => _assignmentShortLabel;
        private set => SetProperty(ref _assignmentShortLabel, value);
    }

    private string _shortName = string.Empty;
    private string _assignmentShortLabel = string.Empty;

    public string IpAddressDisplay
    {
        get => _ipAddressDisplay;
        private set => SetProperty(ref _ipAddressDisplay, value);
    }

    public string AssignmentLabel
    {
        get => _assignmentLabel;
        private set => SetProperty(ref _assignmentLabel, value);
    }

    public string AdapterIconGlyph
    {
        get => _adapterIconGlyph;
        private set => SetProperty(ref _adapterIconGlyph, value);
    }

    public Brush BadgeBackground
    {
        get => _badgeBackground;
        private set => SetProperty(ref _badgeBackground, value);
    }

    public Brush BadgeForeground
    {
        get => _badgeForeground;
        private set => SetProperty(ref _badgeForeground, value);
    }

    public bool Matches(NetworkAdapterInfo info) =>
        Name == info.Name
        && IpAddressDisplay == (string.IsNullOrWhiteSpace(info.IPv4Address) ? "—" : info.IPv4Address)
        && _assignmentMode == info.AssignmentMode
        && IsPrimary == info.IsPrimary
        && IsUsbLan == info.IsUsbLan
        && LinkStatusLabel == (info.LinkStatusLabel ?? string.Empty);

    public void UpdateFrom(NetworkAdapterInfo info)
    {
        _assignmentMode = info.AssignmentMode;
        IsPrimary = info.IsPrimary;
        IsUsbLan = info.IsUsbLan;
        IsVirtual = info.IsVirtual;
        ConfigurationIndex = info.ConfigurationIndex;
        SubnetMask = info.SubnetMask;
        DefaultGateway = info.DefaultGateway;
        DnsServers = info.DnsServers;
        LinkStatusLabel = info.LinkStatusLabel ?? string.Empty;
        Name = info.Name;
        ShortName = GetShortName(info);
        IpAddressDisplay = string.IsNullOrWhiteSpace(info.IPv4Address) ? "—" : info.IPv4Address;
        AssignmentLabel = GetAssignmentLabel(info);
        AssignmentShortLabel = GetAssignmentShortLabel(info);
        AdapterIconGlyph = GetAdapterIconGlyph(info.Name);
        ApplyBadgeStyle(info);
        ApplyRowBackground(info);
        OnPropertyChanged(nameof(IsStaticAssignment));
        OnPropertyChanged(nameof(IsPrimary));
        OnPropertyChanged(nameof(IsUsbLan));
        OnPropertyChanged(nameof(IsVirtual));
        OnPropertyChanged(nameof(ConfigurationIndex));
        OnPropertyChanged(nameof(SubnetMask));
        OnPropertyChanged(nameof(DefaultGateway));
        OnPropertyChanged(nameof(DnsServers));
        OnPropertyChanged(nameof(CanOpenWindowsNetwork));
        OnPropertyChanged(nameof(CanOpenWindowsNetworkVisibility));
        OnPropertyChanged(nameof(AssignmentMode));
        OnPropertyChanged(nameof(RowBackground));
        NotifyMiniLayoutVisibilityChanged();
    }

    public void CopyDisplayFrom(NetworkAdapterItemViewModel source)
    {
        _assignmentMode = source._assignmentMode;
        IsPrimary = source.IsPrimary;
        IsUsbLan = source.IsUsbLan;
        IsVirtual = source.IsVirtual;
        ConfigurationIndex = source.ConfigurationIndex;
        SubnetMask = source.SubnetMask;
        DefaultGateway = source.DefaultGateway;
        DnsServers = source.DnsServers;
        _linkStatusLabel = source._linkStatusLabel;
        Name = source.Name;
        ShortName = source.ShortName;
        IpAddressDisplay = source.IpAddressDisplay;
        AssignmentLabel = source.AssignmentLabel;
        AssignmentShortLabel = source.AssignmentShortLabel;
        AdapterIconGlyph = source.AdapterIconGlyph;
        BadgeBackground = source.BadgeBackground;
        BadgeForeground = source.BadgeForeground;
        RowBackground = source.RowBackground;
        OnPropertyChanged(nameof(IsStaticAssignment));
        OnPropertyChanged(nameof(IsPrimary));
        OnPropertyChanged(nameof(IsUsbLan));
        OnPropertyChanged(nameof(IsVirtual));
        OnPropertyChanged(nameof(ConfigurationIndex));
        OnPropertyChanged(nameof(SubnetMask));
        OnPropertyChanged(nameof(DefaultGateway));
        OnPropertyChanged(nameof(DnsServers));
        OnPropertyChanged(nameof(CanOpenWindowsNetwork));
        OnPropertyChanged(nameof(CanOpenWindowsNetworkVisibility));
        OnPropertyChanged(nameof(AssignmentMode));
        OnPropertyChanged(nameof(RowBackground));
        OnPropertyChanged(nameof(LinkStatusLabel));
        OnPropertyChanged(nameof(LinkStatusVisibility));
        NotifyMiniLayoutVisibilityChanged();
    }

    private void NotifyMiniLayoutVisibilityChanged()
    {
        OnPropertyChanged(nameof(UseMiniUsbLanLinkStack));
        OnPropertyChanged(nameof(MiniUsbLanLinkStackVisibility));
        OnPropertyChanged(nameof(MiniInlineShortNameVisibility));
        OnPropertyChanged(nameof(MiniInlineLinkStatusVisibility));
    }

    private void ApplyRowBackground(NetworkAdapterInfo info)
    {
        if (info.AssignmentMode == IpAssignmentMode.Static)
        {
            RowBackground = GetBrush(
                Application.Current.Resources,
                "SystemFillColorCautionBackgroundBrush",
                Windows.UI.Color.FromArgb(40, 255, 140, 0));
        }
        else if (info.IsPrimary)
        {
            RowBackground = GetBrush(
                Application.Current.Resources,
                "ControlFillColorSecondaryBrush",
                Windows.UI.Color.FromArgb(255, 60, 60, 60));
        }
        else
        {
            RowBackground = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private static string GetShortName(NetworkAdapterInfo info)
    {
        if (info.IsUsbLan)
        {
            return "USB LAN";
        }

        return GetShortName(info.Name);
    }

    private static string GetShortName(string name)
    {
        if (name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Wireless", StringComparison.OrdinalIgnoreCase)
            || name.Contains("WLAN", StringComparison.OrdinalIgnoreCase))
        {
            return "Wi-Fi";
        }

        if (name.Contains("Ethernet", StringComparison.OrdinalIgnoreCase)
            || name.Contains("イーサネット", StringComparison.OrdinalIgnoreCase))
        {
            return "有線";
        }

        return name.Length > 16 ? name[..16] + "…" : name;
    }

    private static string GetAdapterIconGlyph(string name)
    {
        if (name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Wireless", StringComparison.OrdinalIgnoreCase)
            || name.Contains("WLAN", StringComparison.OrdinalIgnoreCase))
        {
            return "\uE701";
        }

        if (name.Contains("Ethernet", StringComparison.OrdinalIgnoreCase)
            || name.Contains("イーサネット", StringComparison.OrdinalIgnoreCase)
            || name.Contains("USB", StringComparison.OrdinalIgnoreCase))
        {
            return "\uE839";
        }

        return "\uE968";
    }

    private static string GetAssignmentLabel(NetworkAdapterInfo info)
    {
        if (info.IsUsbLan && info.UsbLanState == UsbLanRecognitionState.Disabled)
        {
            return "認識済み（無効）";
        }

        if (info.IsUsbLan && info.AssignmentMode == IpAssignmentMode.NoIp)
        {
            return "認識済み（IP未取得）";
        }

        return info.AssignmentMode switch
        {
            IpAssignmentMode.Dhcp => "自動 (DHCP)",
            IpAssignmentMode.Static => "手動 (静的)",
            IpAssignmentMode.NoIp => "IP未取得",
            _ => "無効"
        };
    }

    private static string GetAssignmentShortLabel(NetworkAdapterInfo info)
    {
        if (info.IsUsbLan && info.UsbLanState == UsbLanRecognitionState.Disabled)
        {
            return "無効";
        }

        if (info.IsUsbLan && info.AssignmentMode == IpAssignmentMode.NoIp)
        {
            return "認識済";
        }

        return info.AssignmentMode switch
        {
            IpAssignmentMode.Dhcp => "自動",
            IpAssignmentMode.Static => "手動",
            IpAssignmentMode.NoIp => "未取得",
            _ => "無効"
        };
    }

    private void ApplyBadgeStyle(NetworkAdapterInfo info)
    {
        var resources = Application.Current.Resources;

        if (info.IsUsbLan && info.UsbLanState == UsbLanRecognitionState.Disabled)
        {
            BadgeBackground = GetBrush(resources, "ControlFillColorDefaultBrush", Microsoft.UI.Colors.Gray);
            BadgeForeground = GetBrush(resources, "TextFillColorPrimaryBrush", Microsoft.UI.Colors.White);
            return;
        }

        if (info.IsUsbLan && info.AssignmentMode == IpAssignmentMode.NoIp)
        {
            BadgeBackground = GetBrush(resources, "AccentFillColorDefaultBrush", Microsoft.UI.Colors.SteelBlue);
            BadgeForeground = GetBrush(resources, "TextOnAccentFillColorPrimaryBrush", Microsoft.UI.Colors.White);
            return;
        }

        switch (info.AssignmentMode)
        {
            case IpAssignmentMode.Dhcp:
                BadgeBackground = GetBrush(resources, "AccentFillColorDefaultBrush", Microsoft.UI.Colors.DodgerBlue);
                BadgeForeground = GetBrush(resources, "TextOnAccentFillColorPrimaryBrush", Microsoft.UI.Colors.White);
                break;
            case IpAssignmentMode.Static:
                BadgeBackground = GetBrush(resources, "SystemFillColorCautionBrush", Microsoft.UI.Colors.DarkOrange);
                BadgeForeground = GetBrush(resources, "TextOnAccentFillColorPrimaryBrush", Microsoft.UI.Colors.Black);
                break;
            default:
                BadgeBackground = GetBrush(resources, "ControlFillColorDefaultBrush", Microsoft.UI.Colors.Gray);
                BadgeForeground = GetBrush(resources, "TextFillColorPrimaryBrush", Microsoft.UI.Colors.White);
                break;
        }
    }

    private static Brush GetBrush(ResourceDictionary resources, string key, Windows.UI.Color fallbackColor)
    {
        if (resources.TryGetValue(key, out var value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallbackColor);
    }
}
