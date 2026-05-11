using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FanShop.Services;
using FanShop.ViewModels;
using FanShop.Windows;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FanShop;

public partial class App : Application
{
    private SplashScreenWindow? _splashScreen;
    private MainWindowViewModel? _mainWindowViewModel;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            _splashScreen = new SplashScreenWindow();
            _splashScreen.Show();

            _ = InitializeAppAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitializeAppAsync()
    {
        try
        {
            _splashScreen?.ViewModel.UpdateProgress(5);
            var updateService = new UpdateService();
            bool updateAvailable = await updateService.CheckForUpdatesAsync();

            if (updateAvailable)
            {
                _splashScreen?.ViewModel.UpdateProgress(8);

                bool updated = await updateService.UpdateAsync();
                if (updated)
                {
                    var messageBox = new Window
                    {
                        Title = "Обновление FanShop",
                        Width = 400,
                        Height = 150,
                        CanResize = false,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        Content = new StackPanel
                        {
                            Margin = new Thickness(20),
                            Spacing = 15,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "Доступно обновление приложения. После нажатия OK, программа будет перезапущена.",
                                    TextWrapping = TextWrapping.Wrap
                                },
                                new Button
                                {
                                    Content = "OK",
                                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                                }
                            }
                        }
                    };

                    ((Button)((StackPanel)messageBox.Content).Children[1]).Click += (s, e) =>
                    {
                        messageBox.Close();
                        updateService.ExecuteUpdate();
                    };

                    if (_splashScreen != null)
                    {
                        await messageBox.ShowDialog(_splashScreen);
                    }
                    else
                    {
                        messageBox.Show();
                    }
                    return;
                }
            }

            _splashScreen?.ViewModel.UpdateProgress(10);
            await Task.Delay(100);

            await using (var db = new AppDbContext())
            {
                await db.Database.EnsureCreatedAsync();
                try
                {
                    await db.Employees.AnyAsync();
                }
                catch (Microsoft.Data.Sqlite.SqliteException)
                {
                    await db.Database.EnsureDeletedAsync();
                    await db.Database.EnsureCreatedAsync();
                }

                await EnsureColumnAsync(db, "WorkDayEmployees", "IncludeInPass", "INTEGER NOT NULL DEFAULT 1");
                await EnsureColumnAsync(db, "WorkDayEmployees", "IncludeInSalary", "INTEGER NOT NULL DEFAULT 1");
            }

            _mainWindowViewModel = new MainWindowViewModel();
            _mainWindowViewModel.OpenMainTab();

            _splashScreen?.ViewModel.UpdateProgress(30);
            await Task.Delay(100);

            await _mainWindowViewModel.LoadMatchesFromFirebase();
            _splashScreen?.ViewModel.UpdateProgress(60);
            await Task.Delay(100);

            var mainViewModel = _mainWindowViewModel.GetMainViewModel();
            if (mainViewModel != null)
            {
                await mainViewModel.GenerateCalendar(mainViewModel._currentYear, mainViewModel._currentMonth);
                _splashScreen?.ViewModel.UpdateProgress(80);
                await Task.Delay(100);

                await mainViewModel.CheckAndUpdateCalendarAsync();
                _splashScreen?.ViewModel.UpdateProgress(95);
                await Task.Delay(100);
            }

            _mainWindowViewModel.RefreshStatistics();

            _splashScreen?.ViewModel.UpdateProgress(100);
            await Task.Delay(100);

            _splashScreen?.ViewModel.Stop();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                _mainWindow = new MainWindow { DataContext = _mainWindowViewModel };

                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.MainWindow = _mainWindow;
                }

                _mainWindow.Show();
                _splashScreen?.Close();
            });
        }
        catch (Exception ex)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var errorWindow = new Window
                {
                    Title = "Ошибка",
                    Width = 500,
                    Height = 200,
                    CanResize = false,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(20),
                        Spacing = 15,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = $"Ошибка при запуске: {ex.Message}",
                                TextWrapping = TextWrapping.Wrap
                            },
                            new Button
                            {
                                Content = "OK",
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                            }
                        }
                    }
                };

                ((Button)((StackPanel)errorWindow.Content).Children[1]).Click += (s, e) =>
                {
                    errorWindow.Close();
                };

                if (_splashScreen != null)
                {
                    errorWindow.ShowDialog(_splashScreen);
                }
                else
                {
                    errorWindow.Show();
                }
                Console.WriteLine(ex);
            });
        }
        finally
        {
            _splashScreen?.Close();
        }
    }

    private static async Task EnsureColumnAsync(AppDbContext db, string table, string column, string columnDef)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var probe = connection.CreateCommand();
        probe.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await probe.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }
        await reader.CloseAsync();

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {columnDef};";
        await alter.ExecuteNonQueryAsync();
    }
}
