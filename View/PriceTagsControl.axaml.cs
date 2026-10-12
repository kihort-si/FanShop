using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using FanShop.ViewModels;

namespace FanShop.View;

public partial class PriceTagsControl : UserControl
{
    public PriceTagsControl()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AttachedToVisualTree += async (_, _) =>
        { if (DataContext is PriceTagsViewModel vm) await vm.InitializeAsync(); };
    }
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DataContext is PriceTagsViewModel { CanEdit: true } && e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not PriceTagsViewModel { CanEdit: true } vm) return;
        var paths = e.Data.GetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray() ?? [];
        foreach (var path in paths)
        {
            if (Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)) await vm.ImportTxtAsync(path);
            else if (Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) await vm.ImportExcelAsync(path);
        }
        e.Handled = true;
    }
}
