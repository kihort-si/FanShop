using System.Globalization;
using System.Windows.Data;

namespace FanShop.Converters;

public class ShiftToRussian : IValueConverter
{
    public static ShiftToRussian Instance { get; } = new ShiftToRussian();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int shift)
        {
            return $"{shift} {FormatedShifts(shift)}";
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
    
    private string FormatedShifts(int shifts)
    {
        return shifts switch
        {
            int n when (n % 10 == 1 && n % 100 != 11) => "смена",
            int n when (n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 10 || n % 100 >= 20)) => "смены",
            _ => "смен"
        };
    }
}