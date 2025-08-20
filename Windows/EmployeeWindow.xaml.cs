using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace FanShop.Windows
{
    public partial class EmployeeWindow : Window
    {
        public EmployeeWindow()
        {
            InitializeComponent();
        }
        
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }

    public class EmployeeTitleConverter : IValueConverter
    {
        public static EmployeeTitleConverter Instance { get; } = new EmployeeTitleConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "Новый сотрудник";
                
            return "Редактирование сотрудника";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}