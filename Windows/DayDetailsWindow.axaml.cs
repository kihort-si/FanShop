using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FanShop.Windows;

public partial class DayDetailsWindow : Window
{
    public DayDetailsWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
