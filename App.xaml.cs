using System.Windows;
using FanShop.Services;
using FanShop.ViewModels;
using FanShop.Windows;
using Microsoft.EntityFrameworkCore;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

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
    private MainWindowViewModel _mainWindowViewModel;
    
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        _splashScreen = new SplashScreenWindow();
        _splashScreen.Show();
    
        try
        {
            _splashScreen.ViewModel.UpdateProgress(5);
            var updateService = new UpdateService();
            bool updateAvailable = await updateService.CheckForUpdatesAsync();
        
            if (updateAvailable)
            {
                _splashScreen.ViewModel.UpdateProgress(8);
            
                bool updated = await updateService.UpdateAsync();
                if (updated)
                {
                    MessageBox.Show("Доступно обновление приложения. После нажатия OK, программа будет перезапущена.",
                        "Обновление FanShop", MessageBoxButton.OK, MessageBoxImage.Information);
                
                    updateService.ExecuteUpdate();
                    return; 
                }
            }
            
            _splashScreen.ViewModel.UpdateProgress(10);
            await Task.Delay(100); 
    
            _mainWindowViewModel = new MainWindowViewModel();
            _mainWindowViewModel.OpenMainWindowTab(); 
            
            using (var db = new AppDbContext())
            {
                db.Database.Migrate();
            }

            _splashScreen.ViewModel.UpdateProgress(30);
            await Task.Delay(100); 
            
            var mainViewModel = _mainWindowViewModel.GetMainViewModel();
            if (mainViewModel != null)
            {
                await mainViewModel.GenerateCalendar(mainViewModel
                    ._currentYear, mainViewModel._currentMonth);
                _splashScreen.ViewModel.UpdateProgress(80);
                await Task.Delay(100); 
    
                await mainViewModel.CheckAndUpdateCalendarAsync();
                _splashScreen.ViewModel.UpdateProgress(95);
                await Task.Delay(100); 
            }
    
            await _mainWindowViewModel.LoadMatchesFromFirebase();
            _splashScreen.ViewModel.UpdateProgress(60);
            await Task.Delay(100);
    
            DbInitializer.Initialize();
            _mainWindowViewModel.RefreshStatistics();
            
            _splashScreen.ViewModel.UpdateProgress(100);
            await Task.Delay(100); 
    
            _splashScreen.ViewModel.Stop();
    
            var mainWindow = new MainWindow { DataContext = _mainWindowViewModel };
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при запуске: {ex.Message}", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            Console.WriteLine(ex);
        }
        finally
        {
            _splashScreen.Close();
        }
    }
}