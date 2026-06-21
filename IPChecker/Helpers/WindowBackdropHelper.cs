using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace IPChecker.Helpers;

public static class WindowBackdropHelper
{
    public static void Apply(Window window, Panel rootSurface, Control contentSurface)
    {
        window.SystemBackdrop = null;
        var brush = GetSolidBackgroundBrush();

        rootSurface.Background = brush;
        contentSurface.Background = brush;

        App.WriteStartupLog("Backdrop: solid opaque (Mica/Acrylic disabled)");
    }

    internal static Brush GetSolidBackgroundBrush()
    {
        if (Application.Current.Resources.TryGetValue(
                "SolidBackgroundFillColorBaseBrush",
                out var resource)
            && resource is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 32, 32, 32));
    }
}
