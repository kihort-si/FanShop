using Avalonia.Controls;
using FanShop.ViewModels;

namespace FanShop.View;

public partial class ReportAdjustmentControl : UserControl
{
    public ReportAdjustmentControl() => InitializeComponent();
    private void OnReportSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ReportAdjustmentViewModel vm && sender is DataGrid grid)
            vm.SelectedRows = grid.SelectedItems.OfType<SalesReportRowViewModel>().ToArray();
    }
}
