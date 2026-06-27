using Avalonia.Controls;
using Avalonia.Interactivity;
using FanShop.ViewModels;

namespace FanShop.View;

public partial class EmployeeCostAnalyticsControl : UserControl
{
    public EmployeeCostAnalyticsControl()
    {
        InitializeComponent();
    }
    
    private async void ExportButton_Click(object? sender, RoutedEventArgs routedEventArgs)
    {
        if (DataContext is EmployeeCostAnalyticsViewModel vm)
        {
            await vm.ExportToExcelAsync(TopLevel.GetTopLevel(this)!);
        }
    }
}
