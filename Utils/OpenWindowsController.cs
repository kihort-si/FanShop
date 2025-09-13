using System.Windows;
using FanShop.ViewModels;
using Application = System.Windows.Application;

namespace FanShop.Utils;

public static class OpenWindowsController
{
    private static List<Window> _windows = new List<Window>();

    public static void Register(Window window)
    {
        _windows.Add(window);
    }

    public static void Unregister(Window window)
    {
        _windows.Remove(window);
        if (_windows.Count == 0)
        {
            var mainWindowViewModel = Application.Current.MainWindow?.DataContext as MainWindowViewModel;
            mainWindowViewModel?.SetBlackoutMode(false);
        }
    }

    public static bool IsMainWindow()
    {
        return _windows.Count == 0;
    }
}