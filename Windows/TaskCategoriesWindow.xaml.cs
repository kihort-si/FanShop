using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace FanShop.Windows
{
    public partial class TaskCategoriesWindow : Window
    {
        public TaskCategoriesWindow()
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
    
    public class StringToVisibilityConverter : IValueConverter
    {
        public static readonly StringToVisibilityConverter Instance = new();
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public static readonly BooleanToVisibilityConverter Instance = new();
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (Visibility)value == Visibility.Visible;
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