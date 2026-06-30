using Avalonia.Controls;
using Avalonia.Interactivity;
using FanShop.ViewModels;

namespace FanShop.Windows;

public partial class DayTasksWindow : Window
{
    public CalendarDayViewModel? ParentViewModel { get; set; }
    
    public DayTasksWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
