using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;
using FanShop.Services;
using FanTabItem = FanShop.Utils.TabItem;
using FanShop.View;
using FanShop.Windows;

namespace FanShop.ViewModels;

public partial class MainWindowViewModel : BaseViewModel
{
    [ObservableProperty]
    private bool _isMenuOpen;

    [ObservableProperty]
    private bool _isBlackoutMode;

    [ObservableProperty]
    private ObservableCollection<FanTabItem> _openWindows = new();

    [ObservableProperty]
    private FanTabItem? _selectedWindow;

    public bool HasOpenWindows => OpenWindows.Any();

    public MainWindowViewModel()
    {
        OpenWindows = new ObservableCollection<FanTabItem>();
        CloseTabCommand = new RelayCommand<object?>(CloseTab);
    }

    [RelayCommand]
    private void ToggleMenu()
    {
        IsMenuOpen = !IsMenuOpen;
        IsBlackoutMode = !IsBlackoutMode;
    }

    [RelayCommand]
    private void CloseMenu()
    {
        IsMenuOpen = false;
        IsBlackoutMode = false;
    }

    [RelayCommand]
    private async Task LoadMatches()
    {
        await LoadMatchesFromFirebase();
    }

    [RelayCommand]
    private async Task OpenPassTemplate()
    {
        await PassTemplateService.OpenTemplateAsync(GetMainWindow());

        IsMenuOpen = false;
        IsBlackoutMode = false;
    }

    public IRelayCommand<object?> CloseTabCommand { get; }

    public void SetBlackoutMode(bool isBlackout)
    {
        IsBlackoutMode = isBlackout;
    }

    private static Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    public void OpenMainTab()
    {
        var mainWindowTab = new MainControl
        {
            DataContext = new MainViewModel()
        };

        var tabItem = new FanTabItem
        {
            Title = "Главная",
            Content = mainWindowTab,
            IsClosable = false
        };

        OpenTab(tabItem);
    }
    
    public async Task CheckWhatsNew()
    {
        var settings = Settings.Load();

        string currentVersion = UpdateService.GetAppVersion();

        if (settings.LastSeenWhatsNewVersion == currentVersion)
            return;

        await OpenWhatsNewWindow(currentVersion);

        settings.LastSeenWhatsNewVersion = currentVersion;
        settings.Save();
    }
    
    private async Task OpenWhatsNewWindow(string version)
    {
        var vm = new WhatsNewViewModel
        {
            Version = $"Версия {version}",
            Sections = new ObservableCollection<WhatsNewSection>
            {
                new WhatsNewSection
                {
                    Title = "Новый раздел «Ценники 1С»",
                    Description = "Excel-справочник, сканирование с кассы и TXT, неизвестные штрихкоды, передачу в 1С и восстановление сессии. Подробнее в разделе FAQ."
                },
                new WhatsNewSection()
                {
                    Title = "Обновлена визуализация календаря",
                    Description = "Теперь дни предыдущего и следующего месяца выделяются в календаре"
                },
                new WhatsNewSection
                {
                    Title = "Автообновление с проверкой и восстановлением",
                    Description = "Обновления проверяются и при запуске ценников или корректировки отчёта. Поставка включает runtime; перед установкой проверяются файлы и создаётся резервная копия. Новая версия подтверждает успешный запуск, а при ошибке восстанавливается предыдущая. Известные устаревшие компоненты удаляются, пользовательские данные сохраняются."
                }
            }
        };

        var window = new WhatsNewWindow
        {
            DataContext = vm
        };
        
        var owner = GetCurrentOwner();
        if (owner != null)
        {
            await window.ShowDialog(owner);
        }
        else
        {
            window.Show();
        }
    }

    [RelayCommand]
    private async Task OpenPriceTagsTab()
    {
        try
        {
            OpenTab(new FanTabItem
            {
                Title = "Ценники 1С",
                Content = new PriceTagsControl { DataContext = FanShop.Services.PriceTags.PriceTagModule.ViewModel },
                IsClosable = true
            });
        }
        catch (Exception ex) { await DialogService.ShowInfo($"Не удалось открыть раздел ценников. {ex.Message}"); }
    }

    [RelayCommand]
    private async Task OpenReportAdjustmentTab()
    {
        try
        {
            OpenTab(new FanTabItem
            {
                Title = "Корректировка отчёта",
                Content = new ReportAdjustmentControl { DataContext = FanShop.Services.ReportAdjustment.ReportAdjustmentModule.ViewModel },
                IsClosable = true
            });
        }
        catch (Exception ex) { await DialogService.ShowInfo($"Не удалось открыть корректировку отчёта. {ex.Message}"); }
    }

    [RelayCommand]
    private void OpenEmployeeTab()
    {
        var employeeWindowTab = new EmployeeControl
        {
            DataContext = new EmployeeViewModel(this)
        };

        var tabItem = new FanTabItem
        {
            Title = "Сотрудники",
            Content = employeeWindowTab,
            IsClosable = true
        };

        OpenTab(tabItem);
    }

    [RelayCommand]
    private void OpenTaskCategoriesTab()
    {
        var taskCategoriesWindowTab = new TaskCategoriesControl
        {
            DataContext = new TaskCategoriesViewModel(this)
        };

        var tabItem = new FanTabItem
        {
            Title = "Категории задач",
            Content = taskCategoriesWindowTab,
            IsClosable = true
        };

        OpenTab(tabItem);
    }

    [RelayCommand]
    private void OpenSettingsTab()
    {
        var settingsWindowTab = new SettingsControl
        {
            DataContext = new SettingsViewModel(this)
        };

        var tabItem = new FanTabItem
        {
            Title = "Настройки",
            Content = settingsWindowTab,
            IsClosable = true
        };

        OpenTab(tabItem);
    }

    [RelayCommand]
    private void OpenEmployeeCostAnalyticsTab()
    {
        var analyticsTab = new EmployeeCostAnalyticsControl
        {
            DataContext = new EmployeeCostAnalyticsViewModel(this)
        };

        var tabItem = new FanTabItem
        {
            Title = "Аналитика затрат",
            Content = analyticsTab,
            IsClosable = true
        };

        OpenTab(tabItem);
    }

    [RelayCommand]
    private void OpenFaqTab()
    {
        var faqWindowTab = new FaqControl();

        var tabItem = new FanTabItem
        {
            Title = "FAQ",
            Content = faqWindowTab,
            IsClosable = true
        };

        OpenTab(tabItem);
    }

    private void OpenTab(FanTabItem tabItem)
    {
        if (!OpenWindows.Any(t => t.Title == tabItem.Title))
        {
            OpenWindows.Add(tabItem);
        }
        SelectedWindow = OpenWindows.First(t => t.Title == tabItem.Title);
        OnPropertyChanged(nameof(HasOpenWindows));

        IsMenuOpen = false;
        IsBlackoutMode = false;
    }

    private void CloseTab(object? parameter)
    {
        if (parameter is FanTabItem tab)
        {
            var closedTabIndex = OpenWindows.IndexOf(tab);
            var wasSelected = ReferenceEquals(SelectedWindow, tab);

            OpenWindows.Remove(tab);

            if (wasSelected)
            {
                SelectedWindow = OpenWindows.Count == 0
                    ? null
                    : OpenWindows[Math.Min(closedTabIndex, OpenWindows.Count - 1)];
            }

            OnPropertyChanged(nameof(HasOpenWindows));
        }
    }

    public void OpenTabRequest(object? viewModel, UserControl userControl, string title, bool isClosable = true)
    {
        var existingTab = OpenWindows.FirstOrDefault(tab =>
            tab.Content is Control element && element.DataContext == viewModel);

        if (existingTab != null)
        {
            SelectedWindow = existingTab;
        }
        else
        {
            var newTab = new FanTabItem
            {
                Title = title,
                Content = userControl,
                IsClosable = isClosable
            };

            if (newTab.Content is Control element)
            {
                element.DataContext = viewModel;
            }

            OpenWindows.Add(newTab);
            SelectedWindow = newTab;
            OnPropertyChanged(nameof(HasOpenWindows));
        }
    }

    public void CloseTabRequest(object? viewModel, object? fallbackViewModel = null)
    {
        var tabToClose = OpenWindows.FirstOrDefault(tab =>
            tab.Content is Control element && element.DataContext == viewModel);

        if (tabToClose != null)
        {
            CloseTab(tabToClose);
        }

        if (fallbackViewModel != null)
        {
            SelectTabByViewModel(fallbackViewModel);
        }
    }

    private void SelectTabByViewModel(object viewModel)
    {
        var fallbackTab = OpenWindows.FirstOrDefault(tab =>
            tab.Content is Control element && ReferenceEquals(element.DataContext, viewModel));

        if (fallbackTab != null)
        {
            SelectedWindow = fallbackTab;
        }
    }

    public async Task LoadMatchesFromFirebase()
    {
        var mainTab = OpenWindows.FirstOrDefault(w => w.Title == "Главная");

        if (mainTab?.Content is MainControl mainControl &&
            mainControl.DataContext is MainViewModel mainViewModel)
        {
            await mainViewModel.LoadMatchesFromFirebase();
        }
    }

    public void RefreshStatistics()
    {
        var mainTab = OpenWindows.FirstOrDefault(w => w.Title == "Главная");

        if (mainTab?.Content is MainControl mainControl &&
            mainControl.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.RefreshStatistics();
        }
    }

    public MainViewModel? GetMainViewModel()
    {
        var mainTab = OpenWindows.FirstOrDefault(w => w.Title == "Главная");

        if (mainTab?.Content is MainControl mainControl &&
            mainControl.DataContext is MainViewModel mainViewModel)
        {
            return mainViewModel;
        }

        return null;
    }
    
    private static Window? GetCurrentOwner()
    {
        return (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?
            .MainWindow;
    }
}
