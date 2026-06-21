using IPChecker.Models;
using Microsoft.UI.Xaml.Media;

namespace IPChecker.Helpers;

public static class TrayIconHelper
{
    private static readonly Dictionary<TrayIconState, ImageSource> Cache = new();

    public static ImageSource GetIcon(TrayIconState state)
    {
        if (Cache.TryGetValue(state, out var cached))
        {
            return cached;
        }

        var assetFileName = state switch
        {
            TrayIconState.Dhcp => "TrayIconDhcp.ico",
            TrayIconState.Static => "TrayIconStatic.ico",
            _ => "TrayIconNoIp.ico"
        };

        if (!AppAssetHelper.AssetExists(assetFileName))
        {
            assetFileName = "AppIcon.ico";
        }

        var image = AppAssetHelper.GetImageSource(assetFileName);
        Cache[state] = image;
        return image;
    }

    public static TrayIconState FromAssignmentMode(IpAssignmentMode mode) => mode switch
    {
        IpAssignmentMode.Dhcp => TrayIconState.Dhcp,
        IpAssignmentMode.Static => TrayIconState.Static,
        _ => TrayIconState.NoIp
    };
}
