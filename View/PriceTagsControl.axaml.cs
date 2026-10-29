using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Globalization;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using FanShop.ViewModels;

namespace FanShop.View;

public partial class PriceTagsControl : UserControl
{
    public PriceTagsControl()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnCopyKey, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AttachedToVisualTree += async (_, _) =>
        { if (DataContext is PriceTagsViewModel vm) await vm.InitializeAsync(); };
    }
    private async void OnCopyKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0) return;
        if (e.Source is not Control source || source is TextBox) return;
        var grid = source as DataGrid ?? source.FindAncestorOfType<DataGrid>();
        if (grid?.CurrentColumn is not DataGridBoundColumn column || column.ClipboardContentBinding is null || grid.ItemsSource is null) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        var values = new List<string>();
        foreach (var item in grid.ItemsSource.Cast<object>().Where(item => grid.SelectedItems.Contains(item)))
        {
            // Evaluate the displayed binding without DataGrid's CSV quoting.
            var content = new ContentControl { DataContext = item };
            using var subscription = content.Bind(ContentControl.ContentProperty, column.ClipboardContentBinding);
            values.Add(Convert.ToString(content.Content, CultureInfo.CurrentCulture) ?? string.Empty);
        }
        if (values.Count == 0) return;
        e.Handled = true;
        try { await clipboard.SetTextAsync(string.Join(Environment.NewLine, values)); }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"Не удалось скопировать ячейки ценников: {ex}"); }
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
