using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.InkML;
using FanShop.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using Settings = FanShop.Models.Settings;

namespace FanShop.ViewModels;

public partial class EmployeeCostAnalyticsViewModel : BaseViewModel
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly decimal _dailySalary;
    private bool _isInitializing;

    [ObservableProperty]
    private DateTime _startDate;

    [ObservableProperty]
    private DateTime _endDate;

    [ObservableProperty]
    private DateTimeOffset? _startDateSelection;

    [ObservableProperty]
    private DateTimeOffset? _endDateSelection;

    [ObservableProperty]
    private ObservableCollection<CostKpiItem> _kpiItems = new();

    [ObservableProperty]
    private ObservableCollection<ManagementInsight> _managementInsights = new();

    [ObservableProperty]
    private ObservableCollection<MatchCostAnalysis> _matchAnalyses = new();

    [ObservableProperty]
    private ObservableCollection<EmployeeCostSummary> _employeeSummaries = new();

    [ObservableProperty]
    private ObservableCollection<WorkDayCostExplanation> _workDayExplanations = new();

    [ObservableProperty]
    private List<ISeries> _salaryByMonthSeries = new();

    [ObservableProperty]
    private List<Axis> _salaryByMonthXAxes = new();

    [ObservableProperty]
    private List<Axis> _salaryByMonthYAxes = new();

    [ObservableProperty]
    private List<ISeries> _workTypeSeries = new();

    [ObservableProperty]
    private List<ISeries> _matchLoadSeries = new();

    [ObservableProperty]
    private List<Axis> _matchLoadXAxes = new();

    [ObservableProperty]
    private double _matchLoadChartWidth = 900;
    
    private ObservableCollection<LegendItem> MatchLegend { get; set; } = new();

    public EmployeeCostAnalyticsViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _dailySalary = Settings.Load().DailySalary;

        _isInitializing = true;
        EndDate = DateTime.Today;
        StartDate = new DateTime(DateTime.Today.Year - 1, DateTime.Today.Month, DateTime.Today.Day);
        StartDateSelection = new DateTimeOffset(StartDate);
        EndDateSelection = new DateTimeOffset(EndDate);
        _isInitializing = false;

        UpdateAnalytics();
    }

    partial void OnStartDateChanged(DateTime value)
    {
        if (!_isInitializing)
        {
            UpdateAnalytics();
        }
    }

    partial void OnEndDateChanged(DateTime value)
    {
        if (!_isInitializing)
        {
            UpdateAnalytics();
        }
    }

    partial void OnStartDateSelectionChanged(DateTimeOffset? value)
    {
        if (value.HasValue)
        {
            StartDate = value.Value.Date;
        }
    }

    partial void OnEndDateSelectionChanged(DateTimeOffset? value)
    {
        if (value.HasValue)
        {
            EndDate = value.Value.Date;
        }
    }

    [RelayCommand]
    private void UpdateAnalytics()
    {
        var start = StartDate == default
            ? new DateTime(DateTime.Today.Year, 1, 1)
            : StartDate.Date;
        var end = EndDate == default
            ? DateTime.Today
            : EndDate.Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var matches = LoadMatches(start.AddDays(-65), end)
            .OrderBy(match => match.Date)
            .ToList();

        var windows = BuildMatchWindows(matches, start, end);
        var records = LoadWorkRecords(start, end, windows);

        KpiItems = new ObservableCollection<CostKpiItem>(BuildKpis(records, matches));
        MatchAnalyses = new ObservableCollection<MatchCostAnalysis>(BuildMatchAnalyses(windows, records));
        EmployeeSummaries = new ObservableCollection<EmployeeCostSummary>(BuildEmployeeSummaries(records));
        WorkDayExplanations = new ObservableCollection<WorkDayCostExplanation>(
            records.OrderBy(record => record.Date).ThenBy(record => record.EmployeeName));
        ManagementInsights = new ObservableCollection<ManagementInsight>(BuildInsights(records, matches, MatchAnalyses));

        BuildCharts(records, matches, MatchAnalyses);
    }
    
    public async Task ExportToExcelAsync(TopLevel topLevel)
    {
        string defaultName =
            $"Аналитика_{StartDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.xlsx";

        var path = await FileDialogService.SaveExcelFileAsync(
            topLevel,
            defaultName);

        if (path == null)
            return;

        EmployeeAnalyticsExportToExcel.ExportToExcel(
            path,
            StartDate,
            EndDate,
            KpiItems,
            MatchAnalyses,
            EmployeeSummaries,
            WorkDayExplanations,
            ManagementInsights);
    }

    [RelayCommand]
    private void Close()
    {
        _mainWindowViewModel.CloseTabRequest(this);
    }

    private List<MatchAnalysisSource> LoadMatches(DateTime start, DateTime end)
    {
        var filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FanShop",
            "matches.json");

        if (!File.Exists(filePath))
        {
            return new List<MatchAnalysisSource>();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var matches = JsonSerializer.Deserialize<List<MatchInfoDto>>(json) ?? new List<MatchInfoDto>();

            var matchStart = start <= DateTime.MinValue.AddDays(7)
                ? DateTime.MinValue
                : start.AddDays(-7);
            var matchEnd = end >= DateTime.MaxValue.AddDays(-7)
                ? DateTime.MaxValue
                : end.AddDays(7);

            return matches
                .Select(match => new MatchAnalysisSource
                {
                    TeamName = match.TeamName,
                    Date = DateTime.Parse(match.Time).Date
                })
                .Where(match => match.Date >= matchStart && match.Date <= matchEnd)
                .ToList();
        }
        catch
        {
            return new List<MatchAnalysisSource>();
        }
    }

    private static List<MatchPreparationWindow> BuildMatchWindows(
        IReadOnlyList<MatchAnalysisSource> matches,
        DateTime start,
        DateTime end)
    {
        var windows = new List<MatchPreparationWindow>();

        var pressureGroups = BuildPressureGroups(matches);

        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var previousMatch = index > 0 ? matches[index - 1] : null;
            var pressurePeriod = pressureGroups.TryGetValue(index, out var period)
                ? period
                : null;
            var windowStart = match.Date.AddDays(-6);
            var isAfterLongBreak = false;

            if (previousMatch != null)
            {
                windowStart = Max(windowStart, previousMatch.Date.AddDays(1));
                TimeSpan window = match.Date - previousMatch.Date;
                if (window.Days >= 30)
                {
                    windowStart = match.Date.AddDays(-13);
                    isAfterLongBreak = true;
                }
            }
            
            using var context = new AppDbContext();
            var WorkDayEmployee = context.WorkDayEmployee
                .Where(item => item.WorkDay.Date >= windowStart && item.WorkDay.Date <= match.Date)
                .Include(workDayEmployee => workDayEmployee.WorkDay)
                .ToList();

            var firstDayOfWork = match.Date.AddDays(-1);

            foreach (var workDayEmployee in WorkDayEmployee)
            {
                if (workDayEmployee.WorkDay.Date < firstDayOfWork) firstDayOfWork = workDayEmployee.WorkDay.Date;
            }

            windowStart = Max(windowStart, start);
            windowStart = Max(windowStart, firstDayOfWork);
            var windowEnd = Min(match.Date.AddDays(-1), end);

            windows.Add(new MatchPreparationWindow
            {
                Match = match,
                PreviousMatch = previousMatch,
                Start = windowStart,
                End = windowEnd,
                PressurePeriod = pressurePeriod,
                IsAfterLongBreak = isAfterLongBreak
            });
        }

        return windows;
    }

    private List<WorkDayCostExplanation> LoadWorkRecords(
        DateTime start,
        DateTime end,
        IReadOnlyList<MatchPreparationWindow> windows)
    {
        using var context = new AppDbContext();
        var WorkDayEmployee = context.WorkDayEmployee
            .Include(item => item.WorkDay)
            .Include(item => item.Employee)
            .Where(item => item.WorkDay.Date >= start && item.WorkDay.Date <= end)
            .ToList();

        return WorkDayEmployee.Select(item =>
        {
            var date = item.WorkDay.Date.Date;
            var window = windows.FirstOrDefault(candidate => candidate.Contains(date));
            var matchDay = windows.FirstOrDefault(candidate => candidate.Match.Date == date);
            var salaryAmount = item.IncludeInSalary ? CalculateSalary(item.WorkDuration) : 0m;

            return new WorkDayCostExplanation
            {
                Date = date,
                EmployeeName = $"{item.Employee.Surname} {item.Employee.FirstName} {item.Employee.LastName}",
                WorkDuration = item.WorkDuration,
                IncludeInSalary = item.IncludeInSalary,
                IncludeInPass = item.IncludeInPass,
                SalaryAmount = salaryAmount,
                RelatedMatch = window?.Match.Title ?? matchDay?.Match.Title ?? "Не перед матчем",
                WorkType = item.IncludeInSalary ? "В ЗП" : "Вне ЗП",
                Context = BuildContext(date, window, matchDay, item.IncludeInSalary)
            };
        }).ToList();
    }

    private decimal CalculateSalary(string workDuration)
    {
        return workDuration == "Целый день" ? _dailySalary : _dailySalary / 2;
    }

    private static string BuildContext(
        DateTime date,
        MatchPreparationWindow? preparationWindow,
        MatchPreparationWindow? matchDay,
        bool includeInSalary)
    {
        var salaryPart = includeInSalary
            ? "учитывается в зарплате"
            : "работа отмечена как не входящая в зарплату";

        if (preparationWindow != null)
        {
            var daysBeforeMatch = (preparationWindow.Match.Date - date).Days;
            var compression = preparationWindow.IsPressurePeriod
                ? $" Окно входит в напряженный период: между матчами меньше 5 рабочих дней."
                : string.Empty;
            var longBreak = preparationWindow.IsAfterLongBreak
                ? " Подготовка расширена после длинного перерыва в календаре."
                : string.Empty;

            return $"Подготовка к матчу {preparationWindow.Match.Title}: до матча {daysBeforeMatch} дн.; {salaryPart}.{compression}{longBreak}";
        }

        if (matchDay != null)
        {
            return $"День матча {matchDay.Match.Title}; {salaryPart}.";
        }

        return $"Работа вне подготовительных окон матчей; {salaryPart}.";
    }

    private IEnumerable<CostKpiItem> BuildKpis(IReadOnlyList<WorkDayCostExplanation> records, IReadOnlyList<MatchAnalysisSource> matches)
    {
        var totalSalary = records.Sum(record => record.SalaryAmount);
        var uniqueEmployees = records.Select(record => record.EmployeeName).Distinct().Count();
        var salaryShifts = records.Count(record => record.IncludeInSalary);
        var nonSalaryShifts = records.Count(record => !record.IncludeInSalary);
        var matchRelated = records.Count(record => record.RelatedMatch != "Не перед матчем");

        return new[]
        {
            new CostKpiItem("Затраты на ЗП", FormatMoney(totalSalary), "Только смены с включенным флагом «В зарплату»"),
            new CostKpiItem("Смен в ЗП", salaryShifts.ToString(), "Количество оплачиваемых выходов"),
            new CostKpiItem("Смен вне ЗП", nonSalaryShifts.ToString(), "Работы, зафиксированные без начисления зарплаты"),
            new CostKpiItem("Сотрудников", uniqueEmployees.ToString(), "Уникальные сотрудники за период"),
            new CostKpiItem("Матчевых выходов", matchRelated.ToString(), "Смены в подготовительных окнах или в день матча"),
            new CostKpiItem("Матчей", matches.Count(match => match.Date >= StartDate.Date && match.Date <= EndDate.Date).ToString(), "Матчи в выбранном периоде")
        };
    }

    private static IEnumerable<MatchCostAnalysis> BuildMatchAnalyses(
        IReadOnlyList<MatchPreparationWindow> windows,
        IReadOnlyList<WorkDayCostExplanation> records)
    {
        return windows
            .Where(window => window.Match.Date >= records.Select(record => record.Date).DefaultIfEmpty(window.Match.Date).Min().AddDays(-7))
            .Select(window =>
            {
                var windowRecords = records
                    .Where(record => record.RelatedMatch == window.Match.Title)
                    .ToList();

                var uniqueEmployees = windowRecords.Select(record => record.EmployeeName).Distinct().Count();
                var workDays = windowRecords.Select(record => record.Date).Distinct().Count();
                var totalSalary = windowRecords.Sum(record => record.SalaryAmount);
                var pressure = window switch
                {
                    { IsPressurePeriod: true, IsAfterLongBreak: true } =>
                        $"Матч входит в {window.PressurePeriod!.Label.Replace("Серия матчей", "серию матчей")}: между матчами меньше 5 рабочих дней; одновременно это подготовка после длинного перерыва.",
                    { IsPressurePeriod: true } =>
                        $"Матч входит в {window.PressurePeriod!.Label.Replace("Серия матчей", "серию матчей")}: между соседними матчами меньше 5 рабочих дней.",
                    { IsAfterLongBreak: true } =>
                        $"Расширенная подготовка после перерыва {window.DaysSincePreviousMatch} дн.: ожидается повышенный объем поставок, товара и новых коллекций.",
                    _ => $"Стандартное подготовительное окно {window.WindowDays} дн."
                };

                return new MatchCostAnalysis
                {
                    MatchTitle = window.Match.Title,
                    PreparationWindow = window.WindowText,
                    WindowDays = window.WindowDays,
                    WorkDays = workDays,
                    UniqueEmployees = uniqueEmployees,
                    TotalShifts = windowRecords.Count,
                    TotalSalary = totalSalary,
                    TotalSalaryText = FormatMoney(totalSalary),
                    IsCompressed = window.IsCompressed,
                    IsPressurePeriod = window.IsPressurePeriod,
                    PressurePeriodLabel = window.PressurePeriod?.Label ?? string.Empty,
                    IsAfterLongBreak = window.IsAfterLongBreak,
                    Situation = window.Situation,
                    MatchDate = window.Match.Date,
                    Explanation = $"{pressure} Всего {FormatCount(windowRecords.Count, "выход", "выхода", "выходов")}, {FormatCount(uniqueEmployees, "сотрудник", "сотрудника", "сотрудников")}, сумма ЗП: {FormatMoney(totalSalary)}."
                };
            })
            .Where(analysis => analysis.TotalShifts > 0 || analysis.IsCompressed || analysis.IsAfterLongBreak)
            .OrderBy(analysis => analysis.MatchDate)
            .ToList();
    }

    private static IEnumerable<EmployeeCostSummary> BuildEmployeeSummaries(IReadOnlyList<WorkDayCostExplanation> records)
    {
        return records
            .GroupBy(record => record.EmployeeName)
            .Select(group => new EmployeeCostSummary
            {
                EmployeeName = group.Key,
                PaidShifts = group.Count(record => record.IncludeInSalary),
                UnpaidShifts = group.Count(record => !record.IncludeInSalary),
                MatchRelatedShifts = group.Count(record => record.RelatedMatch != "Не перед матчем"),
                NonMatchShifts = group.Count(record => record.RelatedMatch == "Не перед матчем"),
                TotalSalary = group.Sum(record => record.SalaryAmount),
                TotalSalaryText = FormatMoney(group.Sum(record => record.SalaryAmount)),
                Explanation = BuildEmployeeExplanation(group.ToList())
            })
            .OrderByDescending(summary => summary.TotalSalary)
            .ThenBy(summary => summary.EmployeeName)
            .ToList();
    }

    private static string BuildEmployeeExplanation(IReadOnlyList<WorkDayCostExplanation> records)
    {
        var matchRelated = records.Count(record => record.RelatedMatch != "Не перед матчем");
        var nonMatch = records.Count - matchRelated;
        var unpaid = records.Count(record => !record.IncludeInSalary);

        return $"{FormatCount(matchRelated, "выход", "выхода", "выходов")} связано с матчами, {FormatCount(nonMatch, "выход", "выхода", "выходов")} вне матчевых окон, {FormatCount(unpaid, "выход", "выхода", "выходов")} без начисления ЗП.";
    }

    private static IEnumerable<ManagementInsight> BuildInsights(
        IReadOnlyList<WorkDayCostExplanation> records,
        IReadOnlyList<MatchAnalysisSource> matches,
        IReadOnlyList<MatchCostAnalysis> matchAnalyses)
    {
        var insights = new List<ManagementInsight>();
        var compressed = matchAnalyses.Where(match => match.IsPressurePeriod).ToList();
        var longBreaks = matchAnalyses.Where(match => match.IsAfterLongBreak).ToList();

        if (compressed.Any())
        {
            insights.Add(new ManagementInsight
            {
                Title = "Сжатые окна подготовки",
                Description = $"За период найдено {FormatCount(compressed.Count, "матч", "матча", "матчей")} с короткой паузой. В такие окна объем работ концентрируется на меньшем числе дней, поэтому требуется больше сотрудников одновременно.",
                Severity = "Высокий приоритет"
            });
        }

        if (longBreaks.Any())
        {
            insights.Add(new ManagementInsight
            {
                Title = "Подготовка после длинного перерыва",
                Description = $"{FormatCount(longBreaks.Count, "матч", "матча", "матчей")} требуют расширенного окна подготовки после паузы от 30 дней. Отдельный пик работ: поставки, новый товар, коллекции и перезапуск процессов после перерыва.",
                Severity = "Особый контроль"
            });
        }

        var nonMatch = records.Where(record => record.RelatedMatch == "Не перед матчем").ToList();
        if (nonMatch.Any())
        {
            insights.Add(new ManagementInsight
            {
                Title = "Работы вне матчей",
                Description = $"{FormatCount(nonMatch.Count, "выход", "выхода", "выходов")} не попали в подготовительные окна матчей. Их стоит рассматривать отдельно: обслуживание, внеплановые задачи или административная нагрузка.",
                Severity = "Контроль"
            });
        }

        var unpaid = records.Where(record => !record.IncludeInSalary).ToList();
        if (unpaid.Any())
        {
            insights.Add(new ManagementInsight
            {
                Title = "Работы без начисления ЗП",
                Description = $"{FormatCount(unpaid.Count, "выход", "выхода", "выходов")} отмечены как не входящие в зарплату. Они не увеличивают сумму ЗП, но показывают фактическую загрузку сотрудников.",
                Severity = "Пояснение"
            });
        }

        if (!insights.Any())
        {
            insights.Add(new ManagementInsight
            {
                Title = "Нагрузка стабильна",
                Description = "По выбранному периоду не найдено сжатых матчевых окон или внеплановых выходов. Затраты в основном объясняются стандартной подготовкой к матчам.",
                Severity = "Норма"
            });
        }

        return insights;
    }

    private void BuildCharts(
        IReadOnlyList<WorkDayCostExplanation> records,
        IReadOnlyList<MatchAnalysisSource> matches,
        IReadOnlyList<MatchCostAnalysis> matchAnalyses)
    {
        var months = EachMonth(StartDate.Date, EndDate.Date).ToList();
        var monthlyValues = months
            .Select(month => (double)records
                .Where(record => record.Date.Year == month.Year && record.Date.Month == month.Month)
                .Sum(record => record.SalaryAmount))
            .ToArray();
        var monthlyMatches = months
            .Select(month => (double)matches.Count(match => match.Date.Year == month.Year && match.Date.Month == month.Month))
            .ToArray();

        SalaryByMonthSeries = new List<ISeries>
        {
            new ColumnSeries<double>
            {
                Name = "ЗП",
                Values = monthlyValues,
                Fill = new SolidColorPaint(SKColor.Parse("#0A6DAE")),
                Stroke = null
            },
            new LineSeries<double>
            {
                Name = "Матчей",
                Values = monthlyMatches,
                ScalesYAt = 1,
                Stroke = new SolidColorPaint(SKColor.Parse("#FF9800"), 3),
                Fill = null,
                GeometrySize = 9
            }
        };

        SalaryByMonthXAxes = new List<Axis>
        {
            new()
            {
                Labels = months.Select(month => month.ToString("MMM yy")).ToArray(),
                LabelsRotation = 35
            }
        };

        SalaryByMonthYAxes = new List<Axis>
        {
            new()
            {
                Name = "ЗП",
                MinLimit = 0,
                Labeler = value => $"{value:N0}"
            },
            new()
            {
                Name = "Матчи",
                Position = LiveChartsCore.Measure.AxisPosition.End,
                MinLimit = 0,
                MinStep = 1,
                ForceStepToMin = true
            }
        };

        var matchRelated = records.Count(record => record.RelatedMatch != "Не перед матчем");
        var nonMatch = records.Count(record => record.RelatedMatch == "Не перед матчем");
        var unpaid = records.Count(record => !record.IncludeInSalary);

        WorkTypeSeries = new List<ISeries>
        {
            new PieSeries<double> { Name = "Перед матчами", Values = new[] { (double)matchRelated }, Fill = new SolidColorPaint(SKColor.Parse("#009EE1")) },
            new PieSeries<double> { Name = "Вне матчей", Values = new[] { (double)nonMatch }, Fill = new SolidColorPaint(SKColor.Parse("#FF9800")) },
            new PieSeries<double> { Name = "Вне ЗП", Values = new[] { (double)unpaid }, Fill = new SolidColorPaint(SKColor.Parse("#D64545")) }
        };

        var chartMatches = matchAnalyses.OrderBy(match => match.MatchDate).ToList();
        MatchLoadChartWidth = Math.Max(900, chartMatches.Count * 120);

        var bars = new List<MatchBar>();
        
        var pressurePeriodColors = new Dictionary<string, string>();

        var palette = new[]
        {
            "#FF6B6B",
            "#FF9800",
            "#FFC107",
            "#4CAF50",
            "#009688",
            "#2196F3",
            "#9C27B0",
            "#E91E63"
        };

        var nextColor = 0;

        foreach (var match in chartMatches)
        {
            string color;
            string? stroke = null;

            if (match is { IsAfterLongBreak: true, IsPressurePeriod: false })
            {
                color = "#7B61FF";
            }
            else if (match.IsPressurePeriod)
            {
                var period = match.PressurePeriodLabel;

                if (!pressurePeriodColors.ContainsKey(period))
                {
                    pressurePeriodColors[period] =
                        palette[nextColor % palette.Length];

                    nextColor++;
                }

                if (match.IsAfterLongBreak)
                {
                    stroke = "#7B61FF";
                }

                color = pressurePeriodColors[period];
            }
            else
            {
                color = "#0A6DAE";
            }

            bars.Add(new MatchBar
            {
                Value = match.UniqueEmployees,
                Color = color,
                Stroke = stroke
            });
        }
        
        var specialMarkers = chartMatches
            .Select(match =>
                match.IsPressurePeriod && match.IsAfterLongBreak
                    ? (double)match.UniqueEmployees
                    : double.NaN)
            .ToArray();
        
        MatchLegend.Clear();

        MatchLegend.Add(new LegendItem
        {
            Color = "#0A6DAE",
            Text = "Обычный матч"
        });

        MatchLegend.Add(new LegendItem
        {
            Color = "#7B61FF",
            Text = "Матч после большого перерыва"
        });

        foreach (var pair in pressurePeriodColors)
        {
            MatchLegend.Add(new LegendItem
            {
                Color = pair.Value,
                Text = $"Матчи {pair.Key.Replace("Серия матчей ", "")}"
            });
        }

        var values = bars.Select(x => x.Value).ToArray();
        var colors = bars.Select(x => x.Color).ToArray();
        var strokes = bars.Select(x => x.Stroke).ToArray();

        var series = new ColumnSeries<double>
        {
            Name = "Сотрудников",
            Values = values
        };

        series.PointMeasured += point =>
        {
            point.Visual!.Fill =
                new SolidColorPaint(
                    SKColor.Parse(colors[point.Index]));

            var stroke = strokes[point.Index];

            if (!string.IsNullOrEmpty(stroke))
            {
                point.Visual.Stroke =
                    new SolidColorPaint(
                        SKColor.Parse(stroke), 3);
            }
        };
        
        var scatterSeries = new ScatterSeries<double>
        {
            Name = "Длинный перерыв",
            Values = specialMarkers,
            GeometrySize = 18,
            Fill = new SolidColorPaint(SKColor.Parse("#7B61FF")),
            Stroke = new SolidColorPaint(SKColors.DarkViolet, 2)
        };

        MatchLoadSeries = new List<ISeries>
        {
            series,

            new LineSeries<double>
            {
                Name = "Дней подготовки",
                Values = chartMatches
                    .Select(x => (double)x.WindowDays)
                    .ToArray(),

                Stroke = new SolidColorPaint(SKColor.Parse("#666666"), 2),
                Fill = null,
                GeometrySize = 6,
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#666666"), 2)
            },
            
            scatterSeries
        };

        MatchLoadXAxes = new List<Axis>
        {
            new()
            {
                Labels = chartMatches.Select(match => match.MatchTitle).ToArray(),
                LabelsRotation = 45,
                MinStep = 1,
                ForceStepToMin = true,
                LabelsDensity = 1,
                TextSize = 11
            }
        };
    }

    private static IEnumerable<DateTime> EachMonth(DateTime start, DateTime end)
    {
        var current = new DateTime(start.Year, start.Month, 1);
        var last = new DateTime(end.Year, end.Month, 1);

        while (current <= last)
        {
            yield return current;
            current = current.AddMonths(1);
        }
    }

    private static DateTime Max(DateTime left, DateTime right) => left > right ? left : right;

    private static DateTime Min(DateTime left, DateTime right) => left < right ? left : right;

    private static Dictionary<int, PressurePeriodInfo> BuildPressureGroups(IReadOnlyList<MatchAnalysisSource> matches)
    {
        var groups = new Dictionary<int, PressurePeriodInfo>();
        var currentGroup = new List<int>();

        for (var index = 1; index < matches.Count; index++)
        {
            var previous = matches[index - 1];
            var current = matches[index];
            if (CountBusinessDays(previous.Date.AddDays(1), current.Date.AddDays(-1)) >= 5)
            {
                AddPressureGroup(matches, currentGroup, groups);
                currentGroup.Clear();
                continue;
            }

            if (currentGroup.Count == 0)
            {
                currentGroup.Add(index - 1);
            }

            currentGroup.Add(index);
        }

        AddPressureGroup(matches, currentGroup, groups);

        return groups;
    }

    private static void AddPressureGroup(
        IReadOnlyList<MatchAnalysisSource> matches,
        IReadOnlyCollection<int> groupIndexes,
        IDictionary<int, PressurePeriodInfo> groups)
    {
        var indexes = groupIndexes.Distinct().OrderBy(index => index).ToList();
        if (indexes.Count < 2)
        {
            return;
        }

        var start = matches[indexes.First()].Date;
        var end = matches[indexes.Last()].Date;
        var period = new PressurePeriodInfo
        {
            StartDate = start,
            EndDate = end,
            Label = $"Серия матчей {start:dd.MM}-{end:dd.MM}"
        };

        foreach (var index in indexes)
        {
            groups[index] = period;
        }
    }

    private static int CountBusinessDays(DateTime start, DateTime end)
    {
        if (end < start)
        {
            return 0;
        }

        var count = 0;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                count++;
            }
        }

        return count;
    }

    private static string FormatMoney(decimal value) => $"{value:N0} ₽";

    private static string FormatCount(int count, string one, string few, string many)
    {
        var abs = Math.Abs(count);
        var lastTwo = abs % 100;
        var last = abs % 10;

        var form = lastTwo is >= 11 and <= 14
            ? many
            : last switch
            {
                1 => one,
                >= 2 and <= 4 => few,
                _ => many
            };

        return $"{count} {form}";
    }
}

public class CostKpiItem
{
    public CostKpiItem(string title, string value, string description)
    {
        Title = title;
        Value = value;
        Description = description;
    }

    public string Title { get; }
    public string Value { get; }
    public string Description { get; }
}

public class ManagementInsight
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}

public class MatchCostAnalysis
{
    public string MatchTitle { get; set; } = string.Empty;
    public string PreparationWindow { get; set; } = string.Empty;
    public int WindowDays { get; set; }
    public int WorkDays { get; set; }
    public int UniqueEmployees { get; set; }
    public int TotalShifts { get; set; }
    public decimal TotalSalary { get; set; }
    public string TotalSalaryText { get; set; } = string.Empty;
    public bool IsCompressed { get; set; }
    public bool IsPressurePeriod { get; set; }
    public string PressurePeriodLabel { get; set; } = string.Empty;
    public bool IsAfterLongBreak { get; set; }
    public string Situation { get; set; } = string.Empty;
    public DateTime MatchDate { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public IBrush HighlightBackground => IsPressurePeriod
        ? new SolidColorBrush(Color.Parse("#FFF3D6"))
        : IsAfterLongBreak
            ? new SolidColorBrush(Color.Parse("#EEE9FF"))
            : new SolidColorBrush(Color.Parse("#FFFFFF"));
    public IBrush HighlightBorder => IsAfterLongBreak ? new SolidColorBrush(Color.Parse("#8F7CFF"))
            : IsPressurePeriod ? new SolidColorBrush(Color.Parse("#FFB84D")) : new SolidColorBrush(Color.Parse("#D7E4EF"));
}

public class EmployeeCostSummary
{
    public string EmployeeName { get; set; } = string.Empty;
    public int PaidShifts { get; set; }
    public int UnpaidShifts { get; set; }
    public int MatchRelatedShifts { get; set; }
    public int NonMatchShifts { get; set; }
    public decimal TotalSalary { get; set; }
    public string TotalSalaryText { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public class WorkDayCostExplanation
{
    public DateTime Date { get; set; }
    public string DateText => Date.ToString("dd.MM.yyyy");
    public string EmployeeName { get; set; } = string.Empty;
    public string WorkDuration { get; set; } = string.Empty;
    public bool IncludeInSalary { get; set; }
    public bool IncludeInPass { get; set; }
    public decimal SalaryAmount { get; set; }
    public string SalaryAmountText => $"{SalaryAmount:N0} ₽";
    public string RelatedMatch { get; set; } = string.Empty;
    public string WorkType { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
}

internal class MatchAnalysisSource
{
    public string TeamName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Title => $"{Date:dd.MM} {TeamName}";
}

internal class MatchPreparationWindow
{
    public MatchAnalysisSource Match { get; set; } = new();
    public MatchAnalysisSource? PreviousMatch { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public PressurePeriodInfo? PressurePeriod { get; set; }
    public bool IsAfterLongBreak { get; set; }
    public int WindowDays => End >= Start ? (End - Start).Days + 1 : 0;
    public int? DaysSincePreviousMatch => PreviousMatch == null ? null : (Match.Date - PreviousMatch.Date).Days;
    public bool IsCompressed => IsPressurePeriod;
    public bool IsPressurePeriod => PressurePeriod != null;

    public string Situation => IsAfterLongBreak
        ? IsPressurePeriod
            ? $"{PressurePeriod!.Label} и одновременно подготовка после длинного перерыва"
            : "Матч после длинного перерыва"
        : IsPressurePeriod ? PressurePeriod!.Label : "";
    public string WindowText => WindowDays > 0 ? $"{Start:dd.MM}-{End:dd.MM}" : "нет дней подготовки";

    public bool Contains(DateTime date)
    {
        return WindowDays > 0 && date >= Start && date <= End;
    }
}

internal class PressurePeriodInfo
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Label { get; set; } = string.Empty;
}

internal class MatchBar
{
    public double Value { get; set; }
    public string Color { get; set; } = "#0A6DAE";
    public string? Stroke { get; set; }
}

internal class LegendItem
{
    public string Color { get; set; } = "";
    public string Text { get; set; } = "";
}