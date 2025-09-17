using System.Windows;
using FanShop.ViewModels;

namespace FanShop.Windows;

public partial class SplashScreenWindow : Window
{
    public SplashScreenViewModel ViewModel { get; } = new SplashScreenViewModel();

    public SplashScreenWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionTextBlock.Text = $"Версия {version?.Major}.{version?.Minor}.{version?.Build}";
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }
}
