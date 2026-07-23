using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;
using FanShop.Services;
using FanShop.ViewModels;
using FanShop.Windows;
using Microsoft.EntityFrameworkCore;

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
    private Employee? _multiShiftEmployee;
    private string _multiShiftWorkDuration = "Целый день";
    private Position? _multiShiftPosition;
    private readonly Dictionary<DateTime, int> _multiShiftAssignments = new();

    [ObservableProperty]
    private bool _isMultiShiftMode;

    [ObservableProperty]
    private DateTimeOffset? _selectedCalendarMonth;

    [ObservableProperty]
    private int _pickerYear;

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
        _logoHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "FanShop/1.0 (https://github.com/zenit-arena/fanshop; contact@fanshop.local)");
        _logoHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));

        _currentYear = DateTime.Now.Year;
        _currentMonth = DateTime.Now.Month;
        _pickerYear = _currentYear;
        _selectedCalendarMonth = new DateTimeOffset(new DateTime(_currentYear, _currentMonth, 1));

        _ = GenerateCalendar(_currentYear, _currentMonth);
        _lastCalendarUpdateDate = DateTime.Today;
    }

    partial void OnSelectedCalendarMonthChanged(DateTimeOffset? value)
    {
        if (value == null)
            return;

        PickerYear = value.Value.Year;

        if (value.Value.Year == _currentYear && value.Value.Month == _currentMonth)
            return;

        _ = GoToSelectedMonthAsync(value.Value.Year, value.Value.Month);
    }

    private async Task GoToSelectedMonthAsync(int year, int month)
    {
        _currentYear = year;
        _currentMonth = month;
        await GenerateCalendar(year, month);
        NotifyCalendarPeriodChanged();
    }

    [RelayCommand]
    private void PreviousPickerYear() => PickerYear--;

    [RelayCommand]
    private void NextPickerYear() => PickerYear++;

    public async Task SelectCalendarMonthAsync(int month)
    {
        if (month is < 1 or > 12)
            return;

        await GoToSelectedMonthAsync(PickerYear, month);
        SelectedCalendarMonth = new DateTimeOffset(new DateTime(PickerYear, month, 1));
    }

    private void NotifyCalendarPeriodChanged()
    {
        OnPropertyChanged(nameof(CurrentMonthName));
        OnPropertyChanged(nameof(PreviousMonthName));
        OnPropertyChanged(nameof(NextMonthName));
        OnPropertyChanged(nameof(FormattedMonthTitle));
        RefreshStatistics();
    }

    [RelayCommand]
    private async Task StartMultiShiftMode()
    {
        if (IsMultiShiftMode)
        {
            await FinishMultiShiftModeAsync();
            return;
        }

        var owner = GetMainWindow();
        if (owner == null)
            return;

        var employeeViewModel = new EmployeeViewModel();
        var window = new SelectEmployeeWindow
        {
            DataContext = employeeViewModel,
            SelectionOnly = true
        };

        if (!await window.ShowDialog<bool>(owner) ||
            window.SelectedEmployee == null ||
            window.SelectedPosition == null)
            return;

        _multiShiftEmployee = window.SelectedEmployee;
        _multiShiftWorkDuration = window.SelectedWorkDuration;
        _multiShiftPosition = window.SelectedPosition;
        _multiShiftAssignments.Clear();
        IsMultiShiftMode = true;
    }

    public bool TryAddMultiShift(CalendarDayViewModel day)
    {
        if (!IsMultiShiftMode || _multiShiftEmployee == null || _multiShiftPosition == null)
            return false;

        using var context = new AppDbContext();
        var date = day.Date.Date;
        var workDay = context.WorkDays
            .Include(x => x.WorkDayEmployee)
            .FirstOrDefault(x => x.Date.Date == date);

        if (workDay == null)
        {
            workDay = new WorkDay { Date = date };
            context.WorkDays.Add(workDay);
            context.SaveChanges();
        }

        var assignment = workDay.WorkDayEmployee
            .FirstOrDefault(x => x.EmployeeID == _multiShiftEmployee.EmployeeID);
        var salaryService = new SalaryService(context);

        if (assignment == null)
        {
            assignment = new WorkDayEmployee
            {
                WorkDayID = workDay.WorkDayID,
                EmployeeID = _multiShiftEmployee.EmployeeID,
                IncludeInPass = true
            };
            context.WorkDayEmployee.Add(assignment);
        }

        assignment.WorkDuration = _multiShiftWorkDuration;
        assignment.PositionID = _multiShiftPosition.PositionID;
        assignment.IncludeInPass = true;
        assignment.SalaryAtMoment = salaryService.GetSalaryForShift(
            _multiShiftPosition.PositionID, date, _multiShiftWorkDuration);
        context.SaveChanges();

        _multiShiftAssignments[date] = assignment.WorkDayEmployeeID;
        day.AddEmployeeToDay(
            _multiShiftEmployee,
            _multiShiftWorkDuration,
            assignment.WorkDayEmployeeID,
            _multiShiftPosition.PositionName);
        var displayedAssignment = day.Employees
            .FirstOrDefault(x => x.Employee.EmployeeID == _multiShiftEmployee.EmployeeID);
        if (displayedAssignment != null && !displayedAssignment.IncludeInPass)
            displayedAssignment.IncludeInPass = true;
        return true;
    }

    private async Task FinishMultiShiftModeAsync()
    {
        if (_multiShiftEmployee == null || _multiShiftAssignments.Count == 0)
        {
            ResetMultiShiftMode();
            return;
        }

        var owner = GetMainWindow();
        var dialog = new AgreeDialog
        {
            DataContext = new AgreeDialogViewModel
            {
                Title = "Напечатать пропуск сразу?",
                Message = $"Смены добавлены на {_multiShiftAssignments.Count} дн. Хотите распечатать пропуск на все дни эти?"
            }
        };
        var shouldPrint = owner != null && await dialog.ShowDialog<bool>(owner);

        if (shouldPrint && owner != null && await PassTemplateService.EnsureTemplateAsync(owner))
        {
            var employee = new EmployeeWorkInfo { Employee = _multiShiftEmployee, IncludeInPass = true };
            var created = PassDocumentGenerator.CreateWordPassForDates(
                _multiShiftAssignments.Keys.ToList(),
                new ObservableCollection<EmployeeWorkInfo> { employee });

            if (created)
            {
                using var context = new AppDbContext();
                var ids = _multiShiftAssignments.Values.ToList();
                var assignments = context.WorkDayEmployee.Where(x => ids.Contains(x.WorkDayEmployeeID)).ToList();
                foreach (var assignment in assignments)
                    assignment.IncludeInPass = false;
                context.SaveChanges();

                foreach (var day in CalendarDays.Where(x => _multiShiftAssignments.ContainsKey(x.Date.Date)))
                {
                    var item = day.Employees.FirstOrDefault(x => x.Employee.EmployeeID == _multiShiftEmployee.EmployeeID);
                    if (item != null)
                        item.IncludeInPass = false;
                }
            }
        }

        ResetMultiShiftMode();
    }

    private void ResetMultiShiftMode()
    {
        IsMultiShiftMode = false;
        _multiShiftEmployee = null;
        _multiShiftPosition = null;
        _multiShiftAssignments.Clear();
        RefreshStatistics();
    }

    private static Window? GetMainWindow() =>
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    [RelayCommand]
    private async Task GoToPreviousMonth()
    {
        var previousMonth = new DateTime(_currentYear, _currentMonth, 1).AddMonths(-1);
        _currentYear = previousMonth.Year;
        _currentMonth = previousMonth.Month;
        await GenerateCalendar(_currentYear, _currentMonth);
        SelectedCalendarMonth = new DateTimeOffset(new DateTime(_currentYear, _currentMonth, 1));
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
        SelectedCalendarMonth = new DateTimeOffset(new DateTime(_currentYear, _currentMonth, 1));
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
        SelectedCalendarMonth = new DateTimeOffset(new DateTime(_currentYear, _currentMonth, 1));
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

    private static readonly string LogoCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FanShop",
        "logos");

    private DateTime _lastLogoFetchUtc = DateTime.MinValue;
    private static readonly TimeSpan LogoMinInterval = TimeSpan.FromMilliseconds(400);

    private async Task<Bitmap?> LoadLogoBitmapAsync(string teamName, string url)
    {
        var cachePath = GetLogoCachePath(url);
        if (File.Exists(cachePath))
        {
            try
            {
                return new Bitmap(cachePath);
            }
            catch
            {
                File.Delete(cachePath);
            }
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            var sinceLast = DateTime.UtcNow - _lastLogoFetchUtc;
            if (sinceLast < LogoMinInterval)
                await Task.Delay(LogoMinInterval - sinceLast);

            try
            {
                _lastLogoFetchUtc = DateTime.UtcNow;
                using var response = await _logoHttpClient.GetAsync(url);

                if ((int)response.StatusCode == 429 && attempt < 2)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 * (attempt + 1));
                    await Task.Delay(retryAfter);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync();
                Directory.CreateDirectory(LogoCacheDir);
                await File.WriteAllBytesAsync(cachePath, bytes);
                using var stream = new MemoryStream(bytes);
                return new Bitmap(stream);
            }
            catch (Exception ex) when (attempt == 2)
            {
                Console.WriteLine($"Logo load failed for {teamName}: {url}. {ex.Message}");
                return null;
            }
            catch
            {
                await Task.Delay(TimeSpan.FromSeconds(1 + attempt));
            }
        }

        return null;
    }

    private static string GetLogoCachePath(string url)
    {
        var hash = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(url));
        var name = Convert.ToHexString(hash).ToLowerInvariant();
        return Path.Combine(LogoCacheDir, name);
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
