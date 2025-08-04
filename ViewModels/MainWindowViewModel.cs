using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using FanShop.Windows;

namespace FanShop.ViewModels

{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public int _currentYear;
        public int _currentMonth;

        public ObservableCollection<CalendarDayViewModel> CalendarDays { get; set; } = new();

        public string CurrentMonthName => new DateTime(_currentYear, _currentMonth, 1)
            .ToString("MMMM yyyy", new CultureInfo("ru-RU")).ToUpper();

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
            set
            {
                if (_isMenuOpen != value)
                {
                    _isMenuOpen = value;
                    OnPropertyChanged(nameof(IsMenuOpen));
                }
            }
        }
        
        public ICommand ToggleMenuCommand { get; }
        public ICommand CloseMenuCommand { get; }
        
        public ICommand OpenEmployeeWindowCommand { get; }

        public MainWindowViewModel()
        {
            _currentYear = DateTime.Now.Year;
            _currentMonth = DateTime.Now.Month;

            PreviousMonthCommand = new RelayCommand(GoToPreviousMonth);
            NextMonthCommand = new RelayCommand(GoToNextMonth);
            
            ToggleMenuCommand = new RelayCommand(_ => IsMenuOpen = !IsMenuOpen);
            CloseMenuCommand = new RelayCommand(_ => IsMenuOpen = false);
            GenerateCalendar(_currentYear, _currentMonth);
            
            OpenEmployeeWindowCommand = new RelayCommand(OpenEmployeeWindow);
        }

        private void GoToPreviousMonth(object? parameter)
        {
            var previousMonth = new DateTime(_currentYear, _currentMonth, 1).AddMonths(-1);
            _currentYear = previousMonth.Year;
            _currentMonth = previousMonth.Month;
            GenerateCalendar(_currentYear, _currentMonth);
            OnPropertyChanged(nameof(CurrentMonthName));
            OnPropertyChanged(nameof(PreviousMonthName));
            OnPropertyChanged(nameof(NextMonthName));
        }

        private void GoToNextMonth(object? parameter)
        {
            var nextMonth = new DateTime(_currentYear, _currentMonth, 1).AddMonths(1);
            _currentYear = nextMonth.Year;
            _currentMonth = nextMonth.Month;
            GenerateCalendar(_currentYear, _currentMonth);
            OnPropertyChanged(nameof(CurrentMonthName));
            OnPropertyChanged(nameof(PreviousMonthName));
            OnPropertyChanged(nameof(NextMonthName));
        }

        private void GenerateCalendar(int year, int month)
        {
            CalendarDays.Clear();

            DateTime firstDayOfMonth = new DateTime(year, month, 1);
            int offset = (int)firstDayOfMonth.DayOfWeek;
            offset = offset == 0 ? 6 : offset - 1;
            int daysInMonth = DateTime.DaysInMonth(year, month);
            
            DateTime lastDayOfMonth = new DateTime(year, month, daysInMonth);
            int endOffset = 7 - ((int)lastDayOfMonth.DayOfWeek == 0 ? 7 : (int)lastDayOfMonth.DayOfWeek);

            int totalDays = daysInMonth + offset + endOffset;

            for (int i = 0; i < totalDays; i++)
            {
                DateTime date = firstDayOfMonth.AddDays(i - offset);
                var calendarDay = new CalendarDayViewModel
                {
                    Date = date,
                    IsCurrentMonth = date.Month == _currentMonth && date.Year == _currentYear
                };

                if (date.Month == month && (date.Day == 1 || date.Day == 11))
                {
                    calendarDay.Employees.Add("Никита");
                    calendarDay.Employees.Add("Александр");
                    calendarDay.Employees.Add("Евгений");
                    calendarDay.Employees.Add("Артём");
                    calendarDay.Employees.Add("Алексей");
                }

                if (date == new DateTime(2025, 8, 3))
                {
                    calendarDay.Match = new MatchInfo
                    {
                        TeamName = "ЦСКА",
                        MatchTime = "18:00",
                        // Time = new BitmapImage(new Uri("pack://application:,,,/Resources/clock.png"))
                    };
                }

                CalendarDays.Add(calendarDay);
            }
        }
        
        private void OpenEmployeeWindow(object? parameter)
        {
            var employeeWindow = new EmployeeWindow
            {
                DataContext = new EmployeeWindowViewModel()
            };
            employeeWindow.ShowDialog();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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