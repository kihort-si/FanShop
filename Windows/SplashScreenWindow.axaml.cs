using Avalonia.Controls;
using FanShop.ViewModels;
using System;

namespace FanShop.Windows;

public partial class SplashScreenWindow : Window
{
    public SplashScreenViewModel ViewModel { get; } = new SplashScreenViewModel();

    public SplashScreenWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }
}
