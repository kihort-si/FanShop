using Avalonia.Controls;
using Avalonia.Interactivity;
using FanShop.ViewModels;

namespace FanShop.View;

public partial class MainControl : UserControl
{
    public MainControl()
    {
        InitializeComponent();
    }

    private async void MonthButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string monthText } ||
            !int.TryParse(monthText, out var month) ||
            DataContext is not MainViewModel viewModel)
            return;

        await viewModel.SelectCalendarMonthAsync(month);
        CalendarPickerButton.Flyout?.Hide();
    }
}
