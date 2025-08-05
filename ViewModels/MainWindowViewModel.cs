using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using FanShop.Services;
using FanShop.Windows;

namespace FanShop.ViewModels

{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public int _currentYear;
        public int _currentMonth;

        private readonly FirebaseService _firebaseService;
        public ObservableCollection<CalendarDayViewModel> CalendarDays { get; set; } = new();
        
        public ObservableCollection<MatchInfo> AllMatches { get; set; } = new ObservableCollection<MatchInfo>();
        

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

            GenerateCalendar(_currentYear, _currentMonth);

            OnPropertyChanged(nameof(AllMatches));
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

                if (date.Month == month && (date.Day == 1 || date.Day == 11))
                {
                    calendarDay.Employees.Add("Никита");
                    calendarDay.Employees.Add("Александр");
                    calendarDay.Employees.Add("Евгений");
                    calendarDay.Employees.Add("Артём");
                    calendarDay.Employees.Add("Алексей");
                }
                
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