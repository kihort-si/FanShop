using Avalonia.Controls;
namespace FanShop.View;
public partial class ReplenishmentControl : UserControl
{
    public ReplenishmentControl()
    {
        InitializeComponent();
        FanShop.Controls.DataGridCellClipboard.Attach(this);
    }
}
