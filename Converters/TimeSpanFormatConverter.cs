using System.Globalization;
using System.Windows.Data;

namespace FanShop.Converters;

public class TimeSpanFormatConverter : IValueConverter
{
    public static TimeSpanFormatConverter Instance { get; } = new TimeSpanFormatConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
        {
            return timeSpan.Hours == 0 ? $"{timeSpan.Minutes} {FormatedMinutes(timeSpan.Minutes)}" : $"{timeSpan.Hours} {FormatedHours(timeSpan.Hours)} {timeSpan.Minutes} {FormatedMinutes(timeSpan.Minutes)}";
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
    
    private string FormatedHours(int hours)
    {
        return hours switch
        {
            int n when (n % 10 == 1 && n % 100 != 11) => "час",
            int n when (n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 10 || n % 100 >= 20)) => "часа",
            _ => "часов"
        };
    }
    
    private string FormatedMinutes(int minutes)
    {
        return minutes switch
        {
            int n when (n % 10 == 1 && n % 100 != 11) => "минута",
            int n when (n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 10 || n % 100 >= 20)) => "минуты",
            _ => "минут"
        };

    }
}