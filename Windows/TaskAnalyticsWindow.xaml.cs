using System.Windows;
using System.Windows.Input;

namespace FanShop.Windows
{
    public partial class TaskAnalyticsWindow : Window
    {
        public TaskAnalyticsWindow()
        {
            InitializeComponent();
            DataContext = new ViewModels.TaskAnalyticsViewModel();
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