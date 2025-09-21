using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using FanShop.Services;
using MessageBox = System.Windows.MessageBox;

namespace FanShop.ViewModels
{
    public class MainViewModel : BaseViewModel
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
        public ICommand GoToTodayCommand { get; }
        public ICommand ToggleCalendarViewModeCommand { get; }
        
        private bool _isEmployeeView;
        
        public bool IsEmployeeView
        {
            get => _isEmployeeView;
            set => SetProperty(ref _isEmployeeView, value);
        }
        
        public int TotalEmployeesCount => _statisticsService.GetTotalEmployeesCount(_currentYear, _currentMonth);
        public int WorkDaysCount => _statisticsService.GetWorkDaysCount(_currentYear, _currentMonth);
        public int TotalShiftCount => _statisticsService.GetTotalShiftCount(_currentYear, _currentMonth);
        public string TotalSalary => _statisticsService.GetTotalSalary(_currentYear, _currentMonth);

        public ObservableCollection<EmployeeStatistic> EmployeeStatistics =>
            _statisticsService.GetEmployeeStatistics(_currentYear, _currentMonth);

        public int MonthMatchesCount => GetMonthMatchesCount();

        public MainViewModel()
        {
            _firebaseService =
                new FirebaseService("https://fanshop-11123-default-rtdb.europe-west1.firebasedatabase.app/");
            _statisticsService = new StatisticsService();

            _currentYear = DateTime.Now.Year;
            _currentMonth = DateTime.Now.Month;

            IsEmployeeView = true;

            GoToTodayCommand = new RelayCommand(GoToToday);
            ToggleCalendarViewModeCommand = new RelayCommand(ToggleCalendarViewMode);

            PreviousMonthCommand = new RelayCommand(GoToPreviousMonth);
            NextMonthCommand = new RelayCommand(GoToNextMonth);
            
            GenerateCalendar(_currentYear, _currentMonth);
            _lastCalendarUpdateDate = DateTime.Today;
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

        private async void GoToToday(object? parameter)
        {
            _currentYear = DateTime.Now.Year;
            _currentMonth = DateTime.Now.Month;
            await GenerateCalendar(_currentYear, _currentMonth);
            OnPropertyChanged(nameof(CurrentMonthName));
            OnPropertyChanged(nameof(PreviousMonthName));
            OnPropertyChanged(nameof(NextMonthName));
            RefreshStatistics();
            OnPropertyChanged(nameof(FormattedMonthTitle));
        }
        
        private async void ToggleCalendarViewMode(object? parameter)
        {
            IsEmployeeView = !IsEmployeeView;
            OnPropertyChanged(nameof(IsEmployeeView));
            await GenerateCalendar(_currentYear, _currentMonth);
        }
        
        public async Task LoadMatchesFromFirebase()
        {
            try
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
                        Logo = new BitmapImage(new Uri(match.Logo)),
                        CanChange = match.CanChange
                    });
                }
                
                SaveMatchesToLocalFile(matches);

                await GenerateCalendar(_currentYear, _currentMonth);

                OnPropertyChanged(nameof(AllMatches));
                OnPropertyChanged(nameof(MonthMatchesCount));
            }
            catch (Exception e)
            {
                if (LoadMatchesFromLocalFile())
                {
                    await GenerateCalendar(_currentYear, _currentMonth);
                    OnPropertyChanged(nameof(AllMatches));
                    OnPropertyChanged(nameof(MonthMatchesCount));
                }
                else
                {
                    MessageBox.Show(
                        "Не удалось установить соединение с интернетом, расписание матчей не загрузилось. Локальная копия данных также недоступна.",
                        "Ошибка загрузки данных",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
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
                    IsCurrentMonth = date.Month == _currentMonth && date.Year == _currentYear,
                    IsEmployeeView = IsEmployeeView,
                    MainViewModel = this
                };

                var matchForThisDay = matchesForMonth.FirstOrDefault(m => DateTime.Parse(m.Time).Date == date.Date);
                if (matchForThisDay != null)
                {
                    calendarDay.Match = matchForThisDay;
                }

                CalendarDays.Add(calendarDay);
            }
        }
        
        private static readonly string MatchesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "FanShop", 
            "matches.json");
        
        private void SaveMatchesToLocalFile(IEnumerable<dynamic> matches)
        {
            try
            {
                var matchDtos = matches.Select(m => new MatchInfoDto
                {
                    TeamName = m.TeamName,
                    Time = m.Time,
                    SartTime = m.Time.Split('T')[1].Substring(0, 5),
                    CanChange = m.CanChange
                }).ToList();
                
                Directory.CreateDirectory(Path.GetDirectoryName(MatchesFilePath)!);
                var json = JsonSerializer.Serialize(matchDtos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(MatchesFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения данных матчей: {ex.Message}");
            }
        }

        private bool LoadMatchesFromLocalFile()
        {
            try
            {
                if (File.Exists(MatchesFilePath))
                {
                    var json = File.ReadAllText(MatchesFilePath);
                    var matches = JsonSerializer.Deserialize<List<MatchInfo>>(json);
            
                    if (matches != null && matches.Any())
                    {
                        AllMatches.Clear();
                        foreach (var match in matches)
                        {
                            AllMatches.Add(match);
                        }
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки данных матчей: {ex.Message}");
            }
            return false;
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
    
    public class MatchInfoDto
    {
        public string TeamName { get; set; }
        public string Time { get; set; }
        public string SartTime { get; set; }
        public bool CanChange { get; set; }
    }
}

