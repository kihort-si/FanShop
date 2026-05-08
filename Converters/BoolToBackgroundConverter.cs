using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace FanShop.Converters;

public class BoolToBackgroundConverter : IValueConverter
{
    public static readonly BoolToBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolean && boolean)
        {
            return new SolidColorBrush(Color.Parse("#D9F0FB"));
        }
        return new SolidColorBrush(Colors.White);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
