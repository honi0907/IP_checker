using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace IPChecker.Helpers;

public static class AppAssetHelper
{
    public static string GetAssetPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
    }

    public static bool AssetExists(string fileName)
    {
        return File.Exists(GetAssetPath(fileName));
    }

    public static ImageSource GetImageSource(string fileName)
    {
        var path = GetAssetPath(fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Asset not found: {path}", path);
        }

        return new BitmapImage(new Uri(path));
    }

    public static string GetAppIconPath() => GetAssetPath("AppIcon.ico");
}
