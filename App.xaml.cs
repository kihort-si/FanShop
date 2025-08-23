using System.Configuration;
using System.Data;
using System.Windows;
using FanShop.Services;
using FanShop.ViewModels;
using FanShop.Windows;
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
    }
    
    private SplashScreenWindow _splashScreen;
    private MainWindowViewModel _mainViewModel;
    
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        _splashScreen = new SplashScreenWindow();
        _splashScreen.Show();
        
        await InitializeMainViewModelAsync();
        
        DbInitializer.Initialize();
        
        _splashScreen.ViewModel.StopLoading();
        
        var mainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };
        
        mainWindow.Show();

        _splashScreen.Close();
        
        MainWindow = mainWindow;
    }
    
    private async Task InitializeMainViewModelAsync()
    {
        _mainViewModel = new MainWindowViewModel();
            
        await _mainViewModel.LoadMatchesFromFirebase();
            
        await _mainViewModel.GenerateCalendar(_mainViewModel._currentYear, _mainViewModel._currentMonth);
            
        await _mainViewModel.CheckAndUpdateCalendarAsync();
            
        _mainViewModel.RefreshStatistics();
            
        await Task.Delay(200);
    }
}