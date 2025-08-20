using System.Windows;
using System.Windows.Input;

namespace FanShop.Windows
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            
            var viewModel = DataContext as ViewModels.SettingsWindowViewModel;
            if (viewModel != null)
            {
                viewModel.CloseRequested += () => Close();
            }
        }
        
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}