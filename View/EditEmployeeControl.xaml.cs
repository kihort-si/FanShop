using System.Globalization;
using System.Windows.Data;
using UserControl = System.Windows.Controls.UserControl;

namespace FanShop.View
{
    public partial class EditEmployeeControl : UserControl
    {
        public EditEmployeeControl()
        {
            InitializeComponent();
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