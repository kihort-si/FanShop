using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FanShop.ViewModels;

namespace FanShop.Converters;

public class DateToTodayColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CalendarDayViewModel calendarDay)
        {
            if (calendarDay.Date.Date == DateTime.Today)
            {
                return Colors.Red;
            }
    
            if (calendarDay.IsCurrentMonth)
            {
                return Colors.Black;
            }
    
            return Colors.Gray;
        }
    
        return Colors.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}