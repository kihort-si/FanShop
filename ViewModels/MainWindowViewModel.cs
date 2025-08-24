using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using FanShop.Models;
using FanShop.Services;
using FanShop.Windows;
using Microsoft.EntityFrameworkCore;
using Application = System.Windows.Application;

namespace FanShop.ViewModels

{
    public class MainWindowViewModel : BaseViewModel
    {
        public int _currentYear;
        public int _currentMonth;

        public int CalendarRows { get; private set; } = 6;

        private readonly FirebaseService _firebaseService;
        private readonly StatisticsService _statisticsService;
        public ObservableCollection<CalendarDayViewModel> CalendarDays { get; set; } = new();

        public ObservableCollection<MatchInfo> AllMatches { get; set; } = new ObservableCollection<MatchInfo>();

        private DateTime _lastCalendarUpdateDate;

        public string CurrentMonthName => new DateTime(_currentYear, _currentMonth, 1)
            .ToString("MMMM yyyy", new CultureInfo("ru-RU")).ToUpper();

        public string FormattedMonthTitle =>
            $"Информация о месяце ({char.ToUpper(CurrentMonthName[0]) + CurrentMonthName.Substring(1).ToLower()})";

        public string PreviousMonthName
        {
            get
            {
                var previousMonth = new DateTime(_currentYear, _currentMonth, 1).AddMonths(-1);
                return previousMonth.ToString("MMMM yyyy", new CultureInfo("ru-RU")).ToUpper();
            }
        }

        public string NextMonthName
        {
            get
            {
                var nextMonth = new DateTime(_currentYear, _currentMonth, 1).AddMonths(1);
                return nextMonth.ToString("MMMM yyyy", new CultureInfo("ru-RU")).ToUpper();
            }
        }

        public ICommand PreviousMonthCommand { get; }
        public ICommand NextMonthCommand { get; }

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

        public int TotalEmployeesCount => _statisticsService.GetTotalEmployeesCount(_currentYear, _currentMonth);
        public int WorkDaysCount => _statisticsService.GetWorkDaysCount(_currentYear, _currentMonth);
        public int TotalShiftCount => _statisticsService.GetTotalShiftCount(_currentYear, _currentMonth);
        public string TotalSalary => _statisticsService.GetTotalSalary(_currentYear, _currentMonth);

        public ObservableCollection<EmployeeStatistic> EmployeeStatistics =>
            _statisticsService.GetEmployeeStatistics(_currentYear, _currentMonth);

        public int MonthMatchesCount => GetMonthMatchesCount();
        public ICommand ToggleMenuCommand { get; }
        public ICommand CloseMenuCommand { get; }
        public ICommand OpenEmployeeWindowCommand { get; }
        public ICommand LoadMatchesCommand { get; }
        public ICommand OpenTaskCategoriesWindowCommand { get; }
        public ICommand OpenSettingsWindowCommand { get; }

        public MainWindowViewModel()
        {
            _firebaseService =
                new FirebaseService("https://fanshop-11123-default-rtdb.europe-west1.firebasedatabase.app/");
            _statisticsService = new StatisticsService();

            _currentYear = DateTime.Now.Year;
            _currentMonth = DateTime.Now.Month;

            PreviousMonthCommand = new RelayCommand(GoToPreviousMonth);
            NextMonthCommand = new RelayCommand(GoToNextMonth);

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
            LoadMatchesFromFirebase();
            GenerateCalendar(_currentYear, _currentMonth);
            _lastCalendarUpdateDate = DateTime.Today;

            OpenEmployeeWindowCommand = new RelayCommand(OpenEmployeeWindow);
            LoadMatchesCommand = new RelayCommand(async _ => await LoadMatchesFromFirebase());
            OpenTaskCategoriesWindowCommand = new RelayCommand(OpenTaskCategoriesWindow);
            OpenSettingsWindowCommand = new RelayCommand(OpenSettingsWindow);
        }

        private async void GoToPreviousMonth(object? parameter)
        {
            var previousMonth = new DateTime(_currentYear, _currentMonth, 1).AddMonths(-1);
            _currentYear = previousMonth.Year;
            _currentMonth = previousMonth.Month;
            await GenerateCalendar(_currentYear, _currentMonth);
            OnPropertyChanged(nameof(CurrentMonthName));
            OnPropertyChanged(nameof(PreviousMonthName));
            OnPropertyChanged(nameof(NextMonthName));
            RefreshStatistics();
            OnPropertyChanged(nameof(FormattedMonthTitle));
        }

        private async void GoToNextMonth(object? parameter)
        {
            var nextMonth = new DateTime(_currentYear, _currentMonth, 1).AddMonths(1);
            _currentYear = nextMonth.Year;
            _currentMonth = nextMonth.Month;
            await GenerateCalendar(_currentYear, _currentMonth);
            OnPropertyChanged(nameof(CurrentMonthName));
            OnPropertyChanged(nameof(PreviousMonthName));
            OnPropertyChanged(nameof(NextMonthName));
            RefreshStatistics();
            OnPropertyChanged(nameof(FormattedMonthTitle));
        }

        public async Task LoadMatchesFromFirebase()
        {
            var matches = await _firebaseService.GetMatchesAsync();

            AllMatches.Clear();

            foreach (var match in matches)
            {
                AllMatches.Add(new MatchInfo
                {
                    TeamName = match.TeamName,
                    Time = match.Time,
                    SartTime = match.Time.Split('T')[1].Substring(0, 5),
                    Logo = new BitmapImage(new Uri(match.Logo))
                });
            }

            await GenerateCalendar(_currentYear, _currentMonth);

            OnPropertyChanged(nameof(AllMatches));
            OnPropertyChanged(nameof(MonthMatchesCount));
        }


        public async Task GenerateCalendar(int year, int month)
        {
            CalendarDays.Clear();

            DateTime firstDayOfMonth = new DateTime(year, month, 1);
            int offset = (int)firstDayOfMonth.DayOfWeek;
            offset = offset == 0 ? 6 : offset - 1;
            int daysInMonth = DateTime.DaysInMonth(year, month);

            DateTime lastDayOfMonth = new DateTime(year, month, daysInMonth);
            int endOffset = 7 - ((int)lastDayOfMonth.DayOfWeek == 0 ? 7 : (int)lastDayOfMonth.DayOfWeek);

            int totalDays = daysInMonth + offset + endOffset;

            CalendarRows = (int)Math.Ceiling((double)totalDays / 7);
            OnPropertyChanged(nameof(CalendarRows));

            var matchesForMonth = AllMatches.Where(m =>
            {
                DateTime matchDate = DateTime.Parse(m.Time);
                return matchDate >= firstDayOfMonth.AddDays(-offset) &&
                       matchDate <= lastDayOfMonth.AddDays(endOffset + 1);
            }).ToList();

            for (int i = 0; i < totalDays; i++)
            {
                DateTime date = firstDayOfMonth.AddDays(i - offset);
                var calendarDay = new CalendarDayViewModel
                {
                    Date = date,
                    IsCurrentMonth = date.Month == _currentMonth && date.Year == _currentYear
                };

                var matchForThisDay = matchesForMonth.FirstOrDefault(m => DateTime.Parse(m.Time).Date == date.Date);
                if (matchForThisDay != null)
                {
                    calendarDay.Match = matchForThisDay;
                }

                CalendarDays.Add(calendarDay);
            }
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
            employeeWindow.ShowDialog();
        }

        private void OpenTaskCategoriesWindow(object? parameter)
        {
            var dayTasksWindow = new TaskCategoriesWindow
            {
                DataContext = new TaskCategoriesWindowViewModel()
            };
            dayTasksWindow.Owner = Application.Current.MainWindow;
            dayTasksWindow.ShowInTaskbar = false;
            dayTasksWindow.ShowDialog();
        }

        private void OpenSettingsWindow(object? parameter)
        {
            var settingsWindow = new SettingsWindow();
            var viewModel = (SettingsWindowViewModel)settingsWindow.DataContext;

            viewModel.CloseRequested += () =>
            {
                settingsWindow.Close();
                RefreshStatistics();
            };
            
            settingsWindow.Owner = Application.Current.MainWindow;
            settingsWindow.ShowInTaskbar = false;
            settingsWindow.ShowDialog();
        }

        private Settings GetSettings()
        {
            return Settings.Load();
        }

        private int GetMonthMatchesCount()
        {
            var firstDayOfMonth = new DateTime(_currentYear, _currentMonth, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            return CalendarDays
                .Where(cd => cd.Date >= firstDayOfMonth && cd.Date <= lastDayOfMonth)
                .Count(cd => cd.HasMatch);
        }

        public async Task CheckAndUpdateCalendarAsync()
        {
            if (_lastCalendarUpdateDate != DateTime.Today)
            {
                await LoadMatchesFromFirebase();
                RefreshStatistics();

                _lastCalendarUpdateDate = DateTime.Today;

                OnPropertyChanged(nameof(CurrentMonthName));
                OnPropertyChanged(nameof(PreviousMonthName));
                OnPropertyChanged(nameof(NextMonthName));
                OnPropertyChanged(nameof(FormattedMonthTitle));
            }
        }

        public void RefreshStatistics()
        {
            OnPropertyChanged(nameof(TotalEmployeesCount));
            OnPropertyChanged(nameof(WorkDaysCount));
            OnPropertyChanged(nameof(MonthMatchesCount));
            OnPropertyChanged(nameof(TotalShiftCount));
            OnPropertyChanged(nameof(TotalSalary));
            OnPropertyChanged(nameof(EmployeeStatistics));
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