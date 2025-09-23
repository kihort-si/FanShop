using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FanShop.Utils;
using FanShop.View;
using FanShop.Windows;
using Application = System.Windows.Application;
using UserControl = System.Windows.Controls.UserControl;

namespace FanShop.ViewModels

{
    public class MainWindowViewModel : BaseViewModel
    {
        private bool _isMenuOpen;

        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set => SetProperty(ref _isMenuOpen, value);
        }
        
        private bool _isBlackoutMode;
        
        public bool IsBlackoutMode
        {
            get => _isBlackoutMode;
            set => SetProperty(ref _isBlackoutMode, value);
        }
        
        public ICommand ToggleMenuCommand { get; }
        public ICommand CloseMenuCommand { get; }
        public ICommand OpenEmployeeTabCommand { get; }
        public ICommand LoadMatchesCommand { get; }
        public ICommand OpenTaskCategoriesTabCommand { get; }
        public ICommand OpenSettingsTabCommand { get; }
        public ICommand OpenFaqTabCommand { get; }

        private ObservableCollection<TabItem> _openWindows;
        private TabItem _selectedWindow;
        
        public ObservableCollection<TabItem> OpenWindows
        {
            get => _openWindows;
            set => SetProperty(ref _openWindows, value);
        }
    
        public TabItem SelectedWindow
        {
            get => _selectedWindow;
            set => SetProperty(ref _selectedWindow, value);
        }
        
        public bool HasOpenWindows => OpenWindows.Any();
    
        public ICommand CloseTabCommand { get; }
        
        public MainWindowViewModel()
        {
            OpenWindows = new ObservableCollection<TabItem>();
            CloseTabCommand = new RelayCommand(CloseTab);

            ToggleMenuCommand = new RelayCommand(_ =>
            {
                IsMenuOpen = !IsMenuOpen;
                IsBlackoutMode = !IsBlackoutMode;
            });
            CloseMenuCommand = new RelayCommand(_ =>
            {
                IsMenuOpen = false;
                IsBlackoutMode = false;
            });

            OpenEmployeeTabCommand = new RelayCommand(OpenEmployeeTab);
            LoadMatchesCommand = new RelayCommand(async _ => await LoadMatchesFromFirebase());
            OpenTaskCategoriesTabCommand = new RelayCommand(OpenTaskCategoriesTab);
            OpenSettingsTabCommand = new RelayCommand(OpenSettingsTab);
            OpenFaqTabCommand = new RelayCommand(OpenFaqTab);
        }
        
        public void SetBlackoutMode(bool isBlackout)
        {
            IsBlackoutMode = isBlackout;
            OnPropertyChanged(nameof(IsBlackoutMode));
        }
        
        public void OpenMainTab()
        {
            var mainWindowTab = new MainControl
            {
                DataContext = new MainViewModel()
            };
            
            var tabItem = new TabItem
            {
                Title = "Главная",
                Content = mainWindowTab,
                IsClosable = false
            };
            
            OpenTab(tabItem);
        }

        private void OpenEmployeeTab(object? parameter)
        {
            var employeeWindowTab = new EmployeeControl
            {
                DataContext = new EmployeeViewModel(this)
            };
            
            var tabItem = new TabItem
            {
                Title = "Сотрудники",
                Content = employeeWindowTab,
                IsClosable = true
            };
            
            OpenTab(tabItem);
        }
        
        private void OpenFaqWindowTab(object? parameter)
        {
            var faqWindowTab = new FaqControl();
            
            var tabItem = new TabItem
            {
                Title = "FAQ",
                Content = faqWindowTab,
                IsClosable = true
            };
            
            OpenWindowTab(tabItem);
        }
        
        private void OpenSettingsTab(object? parameter)
        {
            var settingsWindowTab = new SettingsControl
            {
                DataContext = new SettingsViewModel(this)
            };

            var tabItem = new TabItem
            {
                Title = "Настройки",
                Content = settingsWindowTab,
                IsClosable = true
            };
            
            OpenTab(tabItem);
        }
        
        private void OpenFaqTab(object? parameter)
        {
            var faqWindowTab = new FaqControl();
            
            var tabItem = new TabItem
            {
                Title = "FAQ",
                Content = faqWindowTab,
                IsClosable = true
            };
            
            OpenTab(tabItem);
        }
        
        private void OpenTab(TabItem tabItem)
        {
            if (!OpenWindows.Contains(tabItem))
            {
                OpenWindows.Add(tabItem);
            }
            SelectedWindow = tabItem;
            OnPropertyChanged(nameof(HasOpenWindows));
            
            IsMenuOpen = false;
            IsBlackoutMode = false;
        }
        
        private void CloseTab(object? parameter)
        {
            if (parameter is TabItem tab)
            {
                OpenWindows.Remove(tab);
                OnPropertyChanged(nameof(HasOpenWindows));
            }
        }
        
        public void OpenTabRequest(object? viewModel, UserControl userControl, string title, bool isClosable = true)
        {
            var existingTab = OpenWindows.FirstOrDefault(tab =>
                tab.Content is FrameworkElement element && element.DataContext == viewModel);

            if (existingTab != null)
            {
                SelectedWindow = existingTab;
            }
            else
            {
                var newTab = new TabItem
                {
                    Title = title,
                    Content = userControl,
                    IsClosable = isClosable
                };

                if (newTab.Content is FrameworkElement element)
                {
                    element.DataContext = viewModel;
                }

                OpenWindows.Add(newTab);
                SelectedWindow = newTab;
                OnPropertyChanged(nameof(HasOpenWindows));
            }
        }

        public void CloseTabRequest(object? viewModel)
        {
            var tabToClose = OpenWindows.FirstOrDefault(tab =>
                tab.Content is FrameworkElement element && element.DataContext == viewModel);

            if (tabToClose != null)
            {
                CloseTab(tabToClose);
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

    }
        
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}