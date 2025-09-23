using System.Globalization;
using System.Windows.Data;
using UserControl = System.Windows.Controls.UserControl;

namespace FanShop.View

{
    public partial class EditTaskCategoriesControl : UserControl
    {
        public EditTaskCategoriesControl()
        {
            InitializeComponent();
        }
    }
    
    public class CategoryTitleConverter : IValueConverter
    {
        public static readonly CategoryTitleConverter Instance = new();
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? "Добавить категорию" : "Редактировать категорию";
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}