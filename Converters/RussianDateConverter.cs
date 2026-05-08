using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FanShop.Converters;

public class RussianDateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime date)
        {
            return date.ToString("dd MMMM yyyy", new CultureInfo("ru-RU"));
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}
