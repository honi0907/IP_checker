using Microsoft.UI.Xaml.Data;

namespace IPChecker.Helpers;

public sealed class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool available && available ? 1.0 : 0.4;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
