using Avalonia.Data.Converters;
using Avalonia.Media;
using FanShop.ViewModels;
using System;
using System.Globalization;

namespace FanShop.Converters;

public class DateToTodayColorConverter : IValueConverter
{
    public static readonly DateToTodayColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CalendarDayViewModel calendarDay)
        {
            if (calendarDay.Date.Date == DateTime.Today)
            {
                return new SolidColorBrush(Colors.Red);
            }

            if (calendarDay.IsCurrentMonth)
            {
                return new SolidColorBrush(Colors.Black);
            }

            return new SolidColorBrush(Colors.Gray);
        }

        return new SolidColorBrush(Colors.Black);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
