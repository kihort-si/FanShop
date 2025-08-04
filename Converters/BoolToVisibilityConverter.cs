using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FanShop.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolean && boolean ? Visibility.Visible : Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (Visibility)value == Visibility.Visible;
    }
}