using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FanShop.Converters;

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public static readonly InverseBoolToVisibilityConverter Instance = new InverseBoolToVisibilityConverter();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolean && boolean ? Visibility.Collapsed : Visibility.Visible;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (Visibility)value != Visibility.Visible;
    }
}