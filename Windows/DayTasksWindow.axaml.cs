using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FanShop.Windows;

public partial class DayTasksWindow : Window
{
    public DayTasksWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
