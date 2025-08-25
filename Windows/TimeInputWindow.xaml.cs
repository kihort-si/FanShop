using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace FanShop.Windows
{
    public partial class TimeInputWindow : Window
    {
        public int Hour { get; private set; }
        public int Minute { get; private set; }
        
        private bool _isInitializing = true;

        public TimeInputWindow(int initialHour, int initialMinute)
        {
            InitializeComponent();
            
            _isInitializing = true;
            
            HoursTextBox.Text = initialHour.ToString("D2");
            MinutesTextBox.Text = initialMinute.ToString("D2");
            
            _isInitializing = false;
            
            HoursTextBox.SelectAll();
            HoursTextBox.Focus();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                Hour = int.Parse(HoursTextBox.Text);
                Minute = int.Parse(MinutesTextBox.Text);
                DialogResult = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private bool ValidateInput()
        {
            if (!int.TryParse(HoursTextBox.Text, out int hour) || hour < 0 || hour > 23)
            {
                MessageBox.Show("Часы должны быть в диапазоне от 0 до 23", 
                    "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                HoursTextBox.Focus();
                return false;
            }

            if (!int.TryParse(MinutesTextBox.Text, out int minute) || minute < 0 || minute > 59)
            {
                MessageBox.Show("Минуты должны быть в диапазоне от 0 до 59", 
                    "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                MinutesTextBox.Focus();
                return false;
            }

            return true;
        }

        private void NumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }
        
        private void HoursTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitializing && HoursTextBox.Text.Length == 2)
            {
                MinutesTextBox.SelectAll();
                MinutesTextBox.Focus();
            }
        }
    }
}