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
    
        try
        {
            _splashScreen.ViewModel.UpdateProgress(10);
            await Task.Delay(100); 
    
            _mainViewModel = new MainWindowViewModel();
            _splashScreen.ViewModel.UpdateProgress(30);
            await Task.Delay(100); 
    
            await _mainViewModel.LoadMatchesFromFirebase();
            _splashScreen.ViewModel.UpdateProgress(60);
            await Task.Delay(100);
    
            await _mainViewModel.GenerateCalendar(_mainViewModel._currentYear, _mainViewModel._currentMonth);
            _splashScreen.ViewModel.UpdateProgress(80);
            await Task.Delay(100); 
    
            await _mainViewModel.CheckAndUpdateCalendarAsync();
            _splashScreen.ViewModel.UpdateProgress(95);
            await Task.Delay(100); 
    
            DbInitializer.Initialize();
            _mainViewModel.RefreshStatistics();
            
            _splashScreen.ViewModel.UpdateProgress(100);
            await Task.Delay(100); 
    
            _splashScreen.ViewModel.Stop();
    
            var mainWindow = new MainWindow { DataContext = _mainViewModel };
            mainWindow.Show();
        }
        finally
        {
            _splashScreen.Close();
        }
    }
}