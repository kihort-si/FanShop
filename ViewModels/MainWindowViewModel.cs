using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FanShop.Utils;
using FanShop.View;
using FanShop.Windows;
using Application = System.Windows.Application;

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
        public ICommand OpenEmployeeWindowCommand { get; }
        public ICommand LoadMatchesCommand { get; }
        public ICommand OpenTaskCategoriesWindowCommand { get; }
        public ICommand OpenSettingsWindowCommand { get; }
        public ICommand OpenFaqWindowCommand { get; }

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

            OpenEmployeeWindowCommand = new RelayCommand(OpenEmployeeWindow);
            LoadMatchesCommand = new RelayCommand(async _ => await LoadMatchesFromFirebase());
            OpenTaskCategoriesWindowCommand = new RelayCommand(OpenTaskCategoriesWindow);
            OpenSettingsWindowCommand = new RelayCommand(OpenSettingsWindowTab);
            OpenFaqWindowCommand = new RelayCommand(OpenFaqWindowTab);
        }
        
        public void SetBlackoutMode(bool isBlackout)
        {
            IsBlackoutMode = isBlackout;
            OnPropertyChanged(nameof(IsBlackoutMode));
        }

        private void OpenEmployeeWindow(object? parameter)
        {
            var employeeWindow = new EmployeeWindow
            {
                DataContext = new EmployeeWindowViewModel()
            };
            employeeWindow.Owner = Application.Current.MainWindow;
            employeeWindow.ShowInTaskbar = false;
            employeeWindow.Show();
            OpenWindowsController.Register(employeeWindow);
            IsMenuOpen = false;
        }

        private void OpenTaskCategoriesWindow(object? parameter)
        {
            var dayTasksWindow = new TaskCategoriesWindow
            {
                DataContext = new TaskCategoriesWindowViewModel()
            };
            dayTasksWindow.Owner = Application.Current.MainWindow;
            dayTasksWindow.ShowInTaskbar = false;
            dayTasksWindow.Show();
            OpenWindowsController.Register(dayTasksWindow);
            IsMenuOpen = false;
        }
        
        public void OpenMainWindowTab()
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
            
            OpenWindowTab(tabItem);
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

        public void OpenSettingsWindowTab(object? parameter)
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
            
            OpenWindowTab(tabItem);
        }
        
        private void OpenWindowTab(TabItem tabItem)
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