using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using FanShop.ViewModels;
using FanShop.Windows;

namespace FanShop.Services;

public static class DialogService
{
    public static async Task ShowInfo(string message)
    {
        var dialog = new InfoDialog
        {
            DataContext = new InfoDialogViewModel
            {
                Message = message
            }
        };

        if (Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            await dialog.ShowDialog(desktop.MainWindow);
        }
        else
        {
            dialog.Show();
        }
    }
}