using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Globalization;

namespace FanShop.Controls;

public static class DataGridCellClipboard
{
    public static void Attach(Control owner) => owner.AddHandler(InputElement.KeyDownEvent,
        (_, args) => Copy(owner, args), RoutingStrategies.Tunnel);
    private static async void Copy(Control owner, KeyEventArgs e)
    {
        if (e.Key != Key.C || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0) return;
        if (e.Source is not Control source || source is TextBox) return;
        var grid = source as DataGrid ?? source.FindAncestorOfType<DataGrid>();
        if (grid?.CurrentColumn is not DataGridBoundColumn column || column.ClipboardContentBinding is null || grid.ItemsSource is null) return;
        var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard;
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

}
