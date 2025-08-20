using System.Windows;
using System.Windows.Input;

namespace FanShop.Windows
{
    public partial class DayDetailsWindow : Window
    {
        public DayDetailsWindow()
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
}