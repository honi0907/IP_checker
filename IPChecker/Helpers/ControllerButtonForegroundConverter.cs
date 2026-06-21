using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace IPChecker.Helpers;

public sealed class ControllerButtonForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isPressed = value is bool pressed && pressed;
        var resources = Application.Current.Resources;

        if (isPressed)
        {
            return GetBrush(resources, "TextOnAccentFillColorPrimaryBrush", Microsoft.UI.Colors.White);
        }

        return GetBrush(resources, "TextFillColorPrimaryBrush", Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static Brush GetBrush(ResourceDictionary resources, string key, Windows.UI.Color fallbackColor)
    {
        if (resources.TryGetValue(key, out var resource) && resource is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallbackColor);
    }
}
