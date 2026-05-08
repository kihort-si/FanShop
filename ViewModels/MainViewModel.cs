using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Services;
using FanShop.ViewModels;

namespace FanShop.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    public int _currentYear;
    public int _currentMonth;

    public int CalendarRows { get; private set; } = 6;

    private readonly FirebaseService _firebaseService;
    private readonly StatisticsService _statisticsService;
    private readonly HttpClient _logoHttpClient;
    public ObservableCollection<CalendarDayViewModel> CalendarDays { get; set; } = new();

    public ObservableCollection<MatchInfo> AllMatches { get; set; } = new();

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

    [ObservableProperty]
    private bool _isEmployeeView = true;

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
        _logoHttpClient = new HttpClient();
        _logoHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 FanShop/1.0");
        _logoHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));
        _logoHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
        _logoHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/apng"));
        _logoHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*", 0.8));
        _logoHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.5));
        _logoHttpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en;q=0.8");

        _currentYear = DateTime.Now.Year;
        _currentMonth = DateTime.Now.Month;

        _ = GenerateCalendar(_currentYear, _currentMonth);
        _lastCalendarUpdateDate = DateTime.Today;
    }

    [RelayCommand]
    private async Task GoToPreviousMonth()
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

    [RelayCommand]
    private async Task GoToNextMonth()
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

    [RelayCommand]
    private async Task GoToToday()
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

    [RelayCommand]
    private async Task ToggleCalendarViewMode()
    {
        IsEmployeeView = !IsEmployeeView;
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
                var logoBitmap = await LoadLogoBitmapAsync(match.TeamName, match.Logo);

                AllMatches.Add(new MatchInfo
                {
                    TeamName = match.TeamName,
                    Time = match.Time,
                    SartTime = match.Time.Split('T')[1].Substring(0, 5),
                    Logo = logoBitmap,
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
                // Log error - notification will be handled by the view
                Console.WriteLine($"Error loading matches: {e.Message}");
            }
        }
    }

    private async Task<Bitmap?> LoadLogoBitmapAsync(string teamName, string url)
    {
        try
        {
            return await LoadBitmapWithHeadersAsync(url, null);
        }
        catch (Exception firstEx)
        {
            try
            {
                var referer = new Uri(url).GetLeftPart(UriPartial.Authority) + "/";
                return await LoadBitmapWithHeadersAsync(url, referer);
            }
            catch (Exception secondEx)
            {
                Console.WriteLine($"Logo load failed for {teamName}: {url}. First: {firstEx.Message}. Second: {secondEx.Message}");
                return null;
            }
        }
    }

    private async Task<Bitmap> LoadBitmapWithHeadersAsync(string url, string? referer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(referer))
        {
            request.Headers.Referrer = new Uri(referer);
        }

        using var response = await _logoHttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var imageBytes = await response.Content.ReadAsByteArrayAsync();
        using var stream = new MemoryStream(imageBytes);
        return new Bitmap(stream);
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
    public string TeamName { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string SartTime { get; set; } = string.Empty;
    public bool CanChange { get; set; }
}

public class MatchInfo
{
    public string TeamName { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string SartTime { get; set; } = string.Empty;
    public Bitmap? Logo { get; set; }
    public bool CanChange { get; set; }
}
