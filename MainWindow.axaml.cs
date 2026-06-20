using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FanShop.ViewModels;
using System;

namespace FanShop;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _mainWindowViewModel;

    public MainWindow()
    {
        InitializeComponent();
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionTextBlock.Text = $"Версия {version?.Major}.{version?.Minor}.{version?.Build}";
        Opened += OnWindowOpened;
        Activated += OnWindowActivated;
    }

    private void OnWindowOpened(object? sender, System.EventArgs e)
    {
        _mainWindowViewModel = DataContext as MainWindowViewModel;
    }

    private async void OnWindowActivated(object? sender, EventArgs e)
    {
        if (_mainWindowViewModel?.GetMainViewModel() is MainViewModel mainViewModel)
        {
            await mainViewModel.CheckAndUpdateCalendarAsync();
        }
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetPosition(this).Y < 40 && e.Pointer.Type == PointerType.Mouse)
        {
            BeginMoveDrag(e);
        }
    }
}
