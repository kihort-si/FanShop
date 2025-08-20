using System.Configuration;
using System.Data;
using System.Windows;
using FanShop.Services;
using Application = System.Windows.Application;

namespace FanShop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DbInitializer.Initialize();
    }
}