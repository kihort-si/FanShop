using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FanShop.ViewModels;
using System;

namespace FanShop;

public partial class MainWindow : Window
{
    internal bool SkipWelcome { get; init; }
    private MainWindowViewModel? _mainWindowViewModel;
    private bool _priceTagsShutdownComplete;
    private bool _priceTagsShutdownStarted;

    public MainWindow()
    {
        InitializeComponent();
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionTextBlock.Text = $"Версия {version?.Major}.{version?.Minor}.{version?.Build}";
        Opened += OnWindowOpened;
        Activated += OnWindowActivated;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        _mainWindowViewModel = DataContext as MainWindowViewModel;

        if (!SkipWelcome && !Environment.GetCommandLineArgs().Contains("--price-tags") && DataContext is MainWindowViewModel vm)
        {
            await vm.CheckWhatsNew();
        }
    }

    private async void OnWindowActivated(object? sender, EventArgs e)
    {
        if (!SkipWelcome && !Environment.GetCommandLineArgs().Contains("--price-tags") && _mainWindowViewModel?.GetMainViewModel() is MainViewModel mainViewModel)
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

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (!_priceTagsShutdownComplete)
        {
            e.Cancel = true;
            base.OnClosing(e);
            if (_priceTagsShutdownStarted) return;
            _priceTagsShutdownStarted = true;
            try { await FanShop.Services.PriceTags.PriceTagModule.ShutdownAsync(); }
            finally { _priceTagsShutdownComplete = true; Close(); }
            return;
        }
        base.OnClosing(e);
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
