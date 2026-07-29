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
                await PrepareLegacyDatabaseForMigrationsAsync(db);
                await db.Database.MigrateAsync();
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

    private static readonly string[] LegacyMigrationIds =
    [
        "20250820142259_AddTaskCategory",
        "20250820160455_AddTask",
        "20250820181811_ChangeTask",
        "20250820183424_ChangeTask2",
        "20250824124029_AddTaskAdnTaskCategories",
        "20260629221316_AddEmployeeAnalytics",
        "20260630174547_RenameWorkDayEmployeesBack"
    ];

    private const string ShopMigrationId =
        "20260721083755_AddShopPositionSalaryHistory";

    private const string DefaultWorkplaceMigrationId =
        "20260729214056_AddDefaultWorkplaceFlags";

    private static async Task PrepareLegacyDatabaseForMigrationsAsync(AppDbContext db)
    {
        // Versions up to 2.0.1 created the database with EnsureCreated().
        // Such a database already has the schema, but no EF migration history.
        // Register the schema that is actually present before calling Migrate().
        if (!await TableExistsAsync(db, "Employees"))
            return;

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """);

        if (await TableExistsAsync(db, "WorkDayEmployee"))
        {
            foreach (var migrationId in LegacyMigrationIds)
                await RegisterMigrationAsync(db, migrationId);
        }

        var hasWorkplaceSchema =
            await TableExistsAsync(db, "Shops") &&
            await TableExistsAsync(db, "Positions") &&
            await TableExistsAsync(db, "SalaryHistories") &&
            await ColumnExistsAsync(db, "WorkDayEmployee", "PositionID") &&
            await ColumnExistsAsync(db, "WorkDayEmployee", "SalaryAtMoment");

        if (hasWorkplaceSchema)
            await RegisterMigrationAsync(db, ShopMigrationId);

        var hasDefaultWorkplaceFlags =
            hasWorkplaceSchema &&
            await ColumnExistsAsync(db, "Shops", "IsDefault") &&
            await ColumnExistsAsync(db, "Positions", "IsDefault");

        if (hasDefaultWorkplaceFlags)
            await RegisterMigrationAsync(db, DefaultWorkplaceMigrationId);
    }

    private static async Task RegisterMigrationAsync(
        AppDbContext db,
        string migrationId)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT OR IGNORE INTO "__EFMigrationsHistory"
                 ("MigrationId", "ProductVersion")
             VALUES ({migrationId}, {"8.0.2"});
             """);
    }

    private static async Task<bool> TableExistsAsync(
        AppDbContext db,
        string table)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = $name;
            """;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = table;
        command.Parameters.Add(parameter);

        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task<bool> ColumnExistsAsync(
        AppDbContext db,
        string table,
        string column)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
