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
            using var updateService = new UpdateService();
            var adjustmentStartup = Environment.GetCommandLineArgs().Contains("--report-adjustment");
            var priceTagsStartup = adjustmentStartup || Environment.GetCommandLineArgs().Contains("--price-tags")
                || await FanShop.Services.PriceTags.PriceTagModule.HasSavedWorkAsync();
            var skipOnlineInitialization = priceTagsStartup || FanShop.Services.Updates.UpdateBootstrap.IsHealthCheck;
            bool updateAvailable = await updateService.CheckForUpdatesAsync();

            var updateProgress = new Progress<int>(p => _splashScreen?.ViewModel.UpdateProgress(8 + p * 87 / 100));
            if (updateAvailable && await updateService.UpdateAsync(updateProgress))
            {
                await FanShop.Services.ReportAdjustment.ReportAdjustmentModule.ShutdownAsync();
                await FanShop.Services.PriceTags.PriceTagModule.ShutdownAsync();
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime updatingDesktop)
                    updatingDesktop.Shutdown(0);
                return;
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
                catch (Microsoft.Data.Sqlite.SqliteException ex)
                {
                    throw new InvalidOperationException("Не удалось прочитать базу данных FanShop. База сохранена; проверьте её схему и резервную копию.", ex);
                }

                await EnsureColumnAsync(db, "WorkDayEmployee", "IncludeInPass", "INTEGER NOT NULL DEFAULT 1");
                await EnsureColumnAsync(db, "WorkDayEmployee", "IncludeInSalary", "INTEGER NOT NULL DEFAULT 1");
                await EnsureColumnAsync(db, "Shops", "IsDefault", "INTEGER NOT NULL DEFAULT 0");
                await EnsureColumnAsync(db, "Positions", "IsDefault", "INTEGER NOT NULL DEFAULT 0");
            }

            _mainWindowViewModel = new MainWindowViewModel();
            _mainWindowViewModel.OpenMainTab();

            _splashScreen?.ViewModel.UpdateProgress(30);
            await Task.Delay(100);

            if (!skipOnlineInitialization) await _mainWindowViewModel.LoadMatchesFromFirebase();
            _splashScreen?.ViewModel.UpdateProgress(60);
            await Task.Delay(100);

            var mainViewModel = _mainWindowViewModel.GetMainViewModel();
            if (mainViewModel != null)
            {
                await mainViewModel.GenerateCalendar(mainViewModel._currentYear, mainViewModel._currentMonth);
                _splashScreen?.ViewModel.UpdateProgress(80);
                await Task.Delay(100);

                if (!skipOnlineInitialization) await mainViewModel.CheckAndUpdateCalendarAsync();
                _splashScreen?.ViewModel.UpdateProgress(95);
                await Task.Delay(100);
            }

            _mainWindowViewModel.RefreshStatistics();
            if (adjustmentStartup) _mainWindowViewModel.OpenReportAdjustmentTabCommand.Execute(null);
            else if (priceTagsStartup) _mainWindowViewModel.OpenPriceTagsTabCommand.Execute(null);

            _splashScreen?.ViewModel.UpdateProgress(100);
            await Task.Delay(100);

            _splashScreen?.ViewModel.Stop();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                _mainWindow = new MainWindow { DataContext = _mainWindowViewModel, SkipWelcome = skipOnlineInitialization };

                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.MainWindow = _mainWindow;
                }

                _mainWindow.Show();
                FanShop.Services.Updates.UpdateBootstrap.ConfirmHealthy();
                _splashScreen?.Close();
            });
            await FanShop.Services.Updates.UpdateBootstrap.CleanLegacyFilesAsync();
            if (FanShop.Services.Updates.UpdateBootstrap.IsHealthCheck)
            {
                _ = _mainWindowViewModel.CheckWhatsNew();
                if (!priceTagsStartup)
                {
                    if (_mainWindow is not null) _mainWindow.SkipWelcome = false;
                    _ = RefreshAfterUpdateAsync(_mainWindowViewModel);
                }
            }
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

    private static async Task RefreshAfterUpdateAsync(MainWindowViewModel viewModel)
    {
        try
        {
            await viewModel.LoadMatchesFromFirebase();
            if (viewModel.GetMainViewModel() is { } mainViewModel) await mainViewModel.CheckAndUpdateCalendarAsync();
        }
        catch (Exception ex) { FanShop.Services.Updates.UpdateState.Log("Не обновлено расписание после успешного запуска; приложение продолжает работу.", ex); }
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
