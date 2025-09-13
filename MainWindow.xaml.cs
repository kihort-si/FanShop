using System.Text;
using System.Windows;
using System.Windows.Input;
using FanShop.Utils;
using FanShop.ViewModels;

namespace FanShop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    protected override async void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (DataContext is MainWindowViewModel vm)
        {
            await vm.CheckAndUpdateCalendarAsync();
        }
    }

    private void ClickableArea_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!OpenWindowsController.IsMainWindow())
        {
            e.Handled = true;
            return;
        }
    }
}