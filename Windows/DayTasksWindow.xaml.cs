using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FanShop.ViewModels;
using Application = System.Windows.Application;
using ComboBox = System.Windows.Controls.ComboBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace FanShop.Windows
{
    public partial class DayTasksWindow : Window
    {
        public DayTasksWindow()
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

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var viewModel = DataContext as DayTasksWindowViewModel;
                viewModel?.SaveTaskChanges(e.Row.Item);
            }
        }

        private void DataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var viewModel = DataContext as DayTasksWindowViewModel;
                viewModel?.SaveTaskChanges(e.Row.Item);
            }
        }

        private void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            var comboBox = sender as ComboBox;
            var task = comboBox?.DataContext as Models.DayTask;
            var viewModel = DataContext as DayTasksWindowViewModel;
            
            if (task != null && viewModel != null)
            {
                var dataGrid = GetParent<DataGrid>(comboBox);
                dataGrid?.CommitEdit();
                
                Dispatcher.BeginInvoke(new Action(() => 
                {
                    viewModel.UpdateTaskTitleByCategory(task);
                }));
            }
        }
        
        private T GetParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }
        
        private void TimeTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll(); 
            }
        }
        
        private void TimeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
                bindingExpression?.UpdateSource();
            }
        }
        
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                
                var bindingExpression = textBox?.GetBindingExpression(TextBox.TextProperty);
                bindingExpression?.UpdateSource();
                
                var dataGrid = GetParent<DataGrid>(textBox);
                dataGrid?.CommitEdit();
                
                e.Handled = true;
            }
        }
        
        private void TimeColumn_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) 
            {
                if (sender is TextBlock textBlock && textBlock.DataContext is Models.DayTask task)
                {
                    string bindingPath = textBlock.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path.Path;
                    bool isStartTime = bindingPath == "StartTimeText";
                    bool isEndTime = bindingPath == "EndTimeText";
                    
                    if (!isStartTime && !isEndTime) return;
                    
                    int hours = isStartTime ? task.StartHour : task.EndHour;
                    int minutes = isStartTime ? task.StartMinute : task.EndMinute;
                    
                    var timeInputWindow = new TimeInputWindow(hours, minutes)
                    {
                        Owner = this
                    };
                    
                    if (timeInputWindow.ShowDialog() == true)
                    {
                        if (isStartTime)
                        {
                            task.StartHour = timeInputWindow.Hour;
                            task.StartMinute = timeInputWindow.Minute;
                            task.OnPropertyChanged(nameof(task.StartTimeText));
                            task.OnPropertyChanged(nameof(task.StartTime));
                        }
                        else
                        {
                            task.EndHour = timeInputWindow.Hour;
                            task.EndMinute = timeInputWindow.Minute;
                            task.OnPropertyChanged(nameof(task.EndTimeText));
                            task.OnPropertyChanged(nameof(task.EndTime));
                        }
                        
                        task.OnPropertyChanged(nameof(task.Duration));
                        
                        var viewModel = DataContext as DayTasksWindowViewModel;
                        viewModel?.SaveTaskChanges(task);
                    }
                }
            }
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public static NullToVisibilityConverter Instance { get; } = new NullToVisibilityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class RussianDateConverter : IValueConverter
    {
        public static RussianDateConverter Instance { get; } = new RussianDateConverter();
    
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime date)
            {
                CultureInfo russianCulture = new CultureInfo("ru-RU");
                return string.Format("{0}", date.ToString("dd MMMM yyyy", russianCulture));
            }
            return value;
        }
    
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}