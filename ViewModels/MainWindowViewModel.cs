using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using FanShop.Services;
using FanShop.Windows;
using Microsoft.EntityFrameworkCore;

namespace FanShop.ViewModels

{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public int _currentYear;
        public int _currentMonth;

        public int CalendarRows { get; private set; } = 6;
        
        private readonly FirebaseService _firebaseService;
        public ObservableCollection<CalendarDayViewModel> CalendarDays { get; set; } = new();
        
        public ObservableCollection<MatchInfo> AllMatches { get; set; } = new ObservableCollection<MatchInfo>();
        

        public string CurrentMonthName => new DateTime(_currentYear, _currentMonth, 1)
            .ToString("MMMM yyyy", new CultureInfo("ru-RU")).ToUpper();
        
        public string FormattedMonthTitle => $"Информация о месяце ({char.ToUpper(CurrentMonthName[0]) + CurrentMonthName.Substring(1).ToLower()})";

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
            _firebaseService = new FirebaseService("https://fanshop-11123-default-rtdb.europe-west1.firebasedatabase.app/");
            
            _currentYear = DateTime.Now.Year;
            _currentMonth = DateTime.Now.Month;

            PreviousMonthCommand = new RelayCommand(GoToPreviousMonth);
            NextMonthCommand = new RelayCommand(GoToNextMonth);
            
            ToggleMenuCommand = new RelayCommand(_ => IsMenuOpen = !IsMenuOpen);
            CloseMenuCommand = new RelayCommand(_ => IsMenuOpen = false);
            LoadMatchesFromFirebase();
            GenerateCalendar(_currentYear, _currentMonth);
            
            OpenEmployeeWindowCommand = new RelayCommand(OpenEmployeeWindow);
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
            OnPropertyChanged(nameof(TotalEmployeesCount));
            OnPropertyChanged(nameof(WorkDaysCount));
            OnPropertyChanged(nameof(MonthMatchesCount));
            OnPropertyChanged(nameof(TotalShiftCount));
            OnPropertyChanged(nameof(TotalSalary));
            OnPropertyChanged(nameof(EmployeeStatistics));
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
            OnPropertyChanged(nameof(TotalEmployeesCount));
            OnPropertyChanged(nameof(WorkDaysCount));
            OnPropertyChanged(nameof(MonthMatchesCount));
            OnPropertyChanged(nameof(TotalShiftCount));
            OnPropertyChanged(nameof(TotalSalary));
            OnPropertyChanged(nameof(EmployeeStatistics));
            OnPropertyChanged(nameof(FormattedMonthTitle));
        }
        
        private async Task LoadMatchesFromFirebase()
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


        private async Task GenerateCalendar(int year, int month)
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
                return matchDate >= firstDayOfMonth.AddDays(-offset) && matchDate <= lastDayOfMonth.AddDays(endOffset + 1);
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
        
        private void OpenEmployeeWindow(object? parameter)
        {
            var employeeWindow = new EmployeeWindow
            {
                DataContext = new EmployeeWindowViewModel()
            };
            employeeWindow.ShowDialog();
        }
        
        public class EmployeeStatistic
        {
            public string EmployeeName { get; set; }
            public int WorkDaysCount { get; set; }
            public string TotalSalary { get; set; }
        }
        
        public int TotalEmployeesCount => GetTotalEmployeesCount();
        public int WorkDaysCount => GetWorkDaysCount();
        public int MonthMatchesCount => GetMonthMatchesCount();
        public int TotalShiftCount => GetTotalShiftCount();
        public string TotalSalary => GetTotalSalary();
        public ObservableCollection<EmployeeStatistic> EmployeeStatistics => GetEmployeeStatistics();
        
        private int GetTotalEmployeesCount()
        {
            using var context = new AppDbContext();
            var firstDayOfMonth = new DateTime(_currentYear, _currentMonth, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
            
            return context.WorkDays
                .Where(wd => wd.Date >= firstDayOfMonth && wd.Date <= lastDayOfMonth)
                .SelectMany(wd => wd.WorkDayEmployees)
                .Select(wde => wde.EmployeeID)
                .Distinct()
                .Count();
        }
        
        private int GetWorkDaysCount()
        {
            using var context = new AppDbContext();
            var firstDayOfMonth = new DateTime(_currentYear, _currentMonth, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
            
            return context.WorkDays
                .Where(wd => wd.Date >= firstDayOfMonth && wd.Date <= lastDayOfMonth)
                .Where(wd => wd.WorkDayEmployees.Any())
                .Count();
        }
        
        private int GetMonthMatchesCount()
        {
            var firstDayOfMonth = new DateTime(_currentYear, _currentMonth, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            return CalendarDays
                .Where(cd => cd.Date >= firstDayOfMonth && cd.Date <= lastDayOfMonth)
                .Count(cd => cd.HasMatch);
        }

        private int GetTotalShiftCount()
        {
            using var context = new AppDbContext();
            var firstDayOfMonth = new DateTime(_currentYear, _currentMonth, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            return context.WorkDays
                .Where(wd => wd.Date >= firstDayOfMonth && wd.Date <= lastDayOfMonth)
                .SelectMany(wd => wd.WorkDayEmployees)
                .Count();
        }

        private string GetTotalSalary()
        {
            using var context = new AppDbContext();
            var firstDayOfMonth = new DateTime(_currentYear, _currentMonth, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            var total = context.WorkDays
                .Where(wd => wd.Date >= firstDayOfMonth && wd.Date <= lastDayOfMonth)
                .SelectMany(wd => wd.WorkDayEmployees)
                .Sum(wde => wde.WorkDuration == "Целый день" ? 2500 : 1250);

            return $"{total:N0} руб.";
        }
        
        private ObservableCollection<EmployeeStatistic> GetEmployeeStatistics()
        {
            using var context = new AppDbContext();
            var firstDayOfMonth = new DateTime(_currentYear, _currentMonth, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
        
            var workDayEmployees = context.WorkDays
                .Where(wd => wd.Date >= firstDayOfMonth && wd.Date <= lastDayOfMonth)
                .SelectMany(wd => wd.WorkDayEmployees)
                .Include(wde => wde.Employee)
                .ToList();
        
            var statistics = workDayEmployees
                .GroupBy(wde => new { wde.Employee.FirstName, wde.Employee.Surname })
                .Select(g => new
                {
                    EmployeeName = $"{g.Key.FirstName} {g.Key.Surname}",
                    WorkDaysCount = g.Count(),
                    SalaryAmount = g.Sum(wde => wde.WorkDuration == "Целый день" ? 2500 : 1250)
                })
                .OrderByDescending(x => x.SalaryAmount)
                .Select(x => new EmployeeStatistic
                {
                    EmployeeName = x.EmployeeName,
                    WorkDaysCount = x.WorkDaysCount,
                    TotalSalary = $"{x.SalaryAmount:N0} руб."
                })
                .ToList();
        
            return new ObservableCollection<EmployeeStatistic>(statistics);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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