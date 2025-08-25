using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FanShop.Models;
using FanShop.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using Application = System.Windows.Application;

namespace FanShop.ViewModels
{
    public class TaskAnalyticsViewModel : BaseViewModel
    {
        private DateTime _startDate;
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }
        
        private DateTime _endDate;
        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }
        
        private ObservableCollection<StatisticItem> _statisticItems = new();
        public ObservableCollection<StatisticItem> StatisticItems
        {
            get => _statisticItems;
            set => SetProperty(ref _statisticItems, value);
        }
        
        private List<ISeries> _categorySeries = new();
        public List<ISeries> CategorySeries
        {
            get => _categorySeries;
            set => SetProperty(ref _categorySeries, value);
        }
        
        private List<ISeries> _dayOfWeekSeries = new();
        public List<ISeries> DayOfWeekSeries
        {
            get => _dayOfWeekSeries;
            set => SetProperty(ref _dayOfWeekSeries, value);
        }
        
        private List<Axis> _dayOfWeekXAxes = new();
        public List<Axis> DayOfWeekXAxes
        {
            get => _dayOfWeekXAxes;
            set => SetProperty(ref _dayOfWeekXAxes, value);
        }
        
        private List<ISeries> _hourOfDaySeries = new();
        public List<ISeries> HourOfDaySeries
        {
            get => _hourOfDaySeries;
            set => SetProperty(ref _hourOfDaySeries, value);
        }
        
        private List<Axis> _hourOfDayXAxes = new();
        public List<Axis> HourOfDayXAxes
        {
            get => _hourOfDayXAxes;
            set => SetProperty(ref _hourOfDayXAxes, value);
        }
        
        private ObservableCollection<TaskCategory> _availableCategories = new();
        public ObservableCollection<TaskCategory> AvailableCategories
        {
            get => _availableCategories;
            set => SetProperty(ref _availableCategories, value);
        }
        
        private TaskCategory _selectedCategoryForInterruptions;
        public TaskCategory SelectedCategoryForInterruptions
        {
            get => _selectedCategoryForInterruptions;
            set
            {
                if (SetProperty(ref _selectedCategoryForInterruptions, value))
                {
                    AnalyzeInterruptions();
                }
            }
        }
        
        private ObservableCollection<CategoryInterruption> _interruptions = new();
        public ObservableCollection<CategoryInterruption> Interruptions
        {
            get => _interruptions;
            set => SetProperty(ref _interruptions, value);
        }
        
        public ICommand CloseWindowCommand { get; }
        public ICommand UpdateAnalyticsCommand { get; }
        
        public TaskAnalyticsViewModel()
        {
            EndDate = DateTime.Today;
            StartDate = EndDate.AddMonths(-1);
            
            CloseWindowCommand = new RelayCommand(CloseWindow);
            UpdateAnalyticsCommand = new RelayCommand(UpdateAnalytics);
            
            UpdateAnalytics(null);
        }
        
        private void CloseWindow(object? parameter)
        {
            Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.GetType().Name == "TaskAnalyticsWindow")?.Close();
        }
        
        private void UpdateAnalytics(object? parameter)
        {
            using var context = new AppDbContext();
            
            var tasks = context.DayTasks
                .Where(t => t.Date >= StartDate && t.Date <= EndDate).Include(dayTask => dayTask.Category)
                .ToList();
                
            foreach (var task in tasks)
            {
                if (task.TaskCategoryID.HasValue)
                {
                    task.Category = context.TaskCategories.Find(task.TaskCategoryID);
                }
            }
            
            var categoriesWithTasks = tasks
                .Where(t => t.Category != null)
                .Select(t => t.Category)
                .DistinctBy(c => c.TaskCategoryID)
                .ToList();
            
            AvailableCategories = new ObservableCollection<TaskCategory>(categoriesWithTasks);
            
            GenerateStatistics(tasks);
            GenerateCategoryChart(tasks);
            GenerateDayOfWeekChart(tasks);
            GenerateHourOfDayChart(tasks);
            
            Interruptions = new ObservableCollection<CategoryInterruption>();
        }
        
        private void GenerateStatistics(List<DayTask> tasks)
        {
            var statistics = new ObservableCollection<StatisticItem>();
            
            var daysWithRecords = tasks.Select(t => t.Date.Date).Distinct().Count();
            statistics.Add(new StatisticItem 
            { 
                Label = "Дней с записями:", 
                Value = daysWithRecords.ToString() 
            });
            
            statistics.Add(new StatisticItem 
            { 
                Label = "Всего задач:", 
                Value = tasks.Count.ToString() 
            });
            
            var totalTime = TimeSpan.Zero;
            foreach (var task in tasks)
            {
                totalTime += task.Duration;
            }
            statistics.Add(new StatisticItem 
            { 
                Label = "Общее учтенное время:", 
                Value = FormatTimeSpan(totalTime) 
            });
            
            if (daysWithRecords > 0)
            {
                TimeSpan averageTimePerDay = TimeSpan.FromTicks(totalTime.Ticks / daysWithRecords);
                statistics.Add(new StatisticItem 
                { 
                    Label = "Среднее время в день:", 
                    Value = FormatTimeSpan(averageTimePerDay) 
                });
            }
            
            var longestTaskGroup = tasks
                .GroupBy(t => t.Title)
                .Select(g => new 
                { 
                    Title = g.Key, 
                    TotalDuration = g.Aggregate(TimeSpan.Zero, (sum, task) => sum + task.Duration)
                })
                .OrderByDescending(g => g.TotalDuration)
                .FirstOrDefault();
            
            if (longestTaskGroup != null)
            {
                statistics.Add(new StatisticItem 
                { 
                    Label = "Самая длинная задача:", 
                    Value = $"{longestTaskGroup.Title} ({FormatTimeSpan(longestTaskGroup.TotalDuration)})" 
                });
            }
            
            var mostPopularCategory = tasks
                .Where(t => t.Category != null)
                .GroupBy(t => t.Category.Name)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();
                
            if (mostPopularCategory != null)
            {
                statistics.Add(new StatisticItem 
                { 
                    Label = "Популярная категория:", 
                    Value = $"{mostPopularCategory.Key} ({mostPopularCategory.Count()} задач)" 
                });
            }
            
            var categoryWithMostTime = tasks
                .Where(t => t.Category != null)
                .GroupBy(t => t.Category.Name)
                .Select(g => new 
                { 
                    Category = g.Key, 
                    TotalTime = g.Aggregate(TimeSpan.Zero, (sum, task) => sum + task.Duration) 
                })
                .OrderByDescending(g => g.TotalTime)
                .FirstOrDefault();
                
            if (categoryWithMostTime != null)
            {
                statistics.Add(new StatisticItem 
                { 
                    Label = "Категория (по времени):", 
                    Value = $"{categoryWithMostTime.Category} ({FormatTimeSpan(categoryWithMostTime.TotalTime)})" 
                });
            }
            
            StatisticItems = statistics;
        }
        
        private void AnalyzeInterruptions()
        {
            if (SelectedCategoryForInterruptions == null)
            {
                Interruptions = new ObservableCollection<CategoryInterruption>();
                return;
            }
        
            using var context = new AppDbContext();
            
            var tasks = context.DayTasks
                .Where(t => t.Date >= StartDate && t.Date <= EndDate)
                .OrderBy(t => t.Date)
                .ThenBy(t => t.StartHour * 60 + t.StartMinute).Include(dayTask => dayTask.Category)
                .ToList();
                
            foreach (var task in tasks)
            {
                if (task.TaskCategoryID.HasValue)
                {
                    task.Category = context.TaskCategories.Find(task.TaskCategoryID);
                }
            }
            
            var interruptions = new Dictionary<int, List<TimeSpan>>();
            
            for (int i = 0; i < tasks.Count - 1; i++)
            {
                var currentTask = tasks[i];
                
                if (currentTask.TaskCategoryID == SelectedCategoryForInterruptions.TaskCategoryID)
                {
                    var nextTask = tasks[i + 1];
                    
                    if (nextTask.TaskCategoryID != currentTask.TaskCategoryID)
                    {
                        var currentEndTime = new DateTime(currentTask.Date.Year, currentTask.Date.Month, currentTask.Date.Day,
                                                         currentTask.EndHour, currentTask.EndMinute, 0);
                        var nextStartTime = new DateTime(nextTask.Date.Year, nextTask.Date.Month, nextTask.Date.Day,
                                                       nextTask.StartHour, nextTask.StartMinute, 0);
                        
                        if (currentTask.Date.Date != nextTask.Date.Date)
                            continue;
                        
                        var timeDifference = nextStartTime - currentEndTime;
                        
                        if (timeDifference.TotalMinutes <= 5)
                        {
                            var categoryId = nextTask.TaskCategoryID ?? -1;
                            if (!interruptions.ContainsKey(categoryId))
                            {
                                interruptions[categoryId] = new List<TimeSpan>();
                            }
                            interruptions[categoryId].Add(timeDifference);
                        }
                    }
                }
            }
            
            var interruptionResults = interruptions
                .Select(kvp => 
                {
                    var category = tasks.FirstOrDefault(t => t.TaskCategoryID == kvp.Key)?.Category;
                    return new CategoryInterruption
                    {
                        CategoryName = category?.Name ?? "Без категории",
                        CategoryColor = category?.Color ?? "#CCCCCC",
                        Count = kvp.Value.Count,
                        AverageTimeToInterruption = kvp.Value.Average(t => t.TotalMinutes)
                    };
                })
                .OrderByDescending(i => i.Count)
                .Take(5)
                .ToList();
            
            Interruptions = new ObservableCollection<CategoryInterruption>(interruptionResults);
        }
        
        private void GenerateCategoryChart(List<DayTask> tasks)
        {
            var categoryData = tasks
                .Where(t => t.Category != null)
                .GroupBy(t => new { t.Category.Name, t.Category.Color })
                .Select(g => new 
                { 
                    Category = g.Key.Name, 
                    Color = g.Key.Color, 
                    TotalMinutes = g.Sum(t => t.Duration.TotalMinutes) 
                })
                .OrderByDescending(g => g.TotalMinutes)
                .ToList();
            
                foreach (var x1 in tasks
                             .Where(t => t.Category == null)
                             .GroupBy(t => "Без категории")
                             .Select(g => new
                             {
                                 Category = "Без категории",
                                 Color = "#CCCCCC",
                                 TotalMinutes = g.Sum(t => t.Duration.TotalMinutes)
                             })
                             .OrderByDescending(g => g.TotalMinutes)
                             .ToList())
                    categoryData.Add(x1);

            var series = new List<ISeries>();
            
            var pieValues = new List<ISeries>();
            
            foreach (var category in categoryData)
            {
                var color = SKColor.Parse(category.Color);
                
                pieValues.Add(new PieSeries<double>
                {
                    Values = new double[] { category.TotalMinutes },
                    Name = $"{category.Category} ({FormatMinutes(category.TotalMinutes)})",
                    Fill = new SolidColorPaint(color),
                    Stroke = null,
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsSize = 14
                });
            }
            
            CategorySeries = pieValues;
        }
        
        private void GenerateDayOfWeekChart(List<DayTask> tasks)
        {
            var dayNames = new[] { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };
            
            var dayOfWeekData = tasks
                .GroupBy(t => ((int)t.Date.DayOfWeek + 6) % 7)
                .Select(g => new
                {
                    DayOfWeek = g.Key,
                    TotalMinutes = g.Sum(t => t.Duration.TotalMinutes)
                })
                .OrderBy(d => d.DayOfWeek)
                .ToList();
                
            var completeData = Enumerable.Range(0, 7)
                .Select(day => new
                {
                    DayOfWeek = day,
                    TotalMinutes = dayOfWeekData.FirstOrDefault(d => d.DayOfWeek == day)?.TotalMinutes ?? 0
                })
                .ToList();
                
            var values = completeData.Select(d => d.TotalMinutes).ToArray();
            
            var series = new List<ISeries>
            {
                new ColumnSeries<double>
                {
                    Values = values,
                    Name = "Время",
                    Stroke = null,
                    Fill = new SolidColorPaint(SKColor.Parse("#009EE1")),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsFormatter = point => FormatMinutes(point.Coordinate.PrimaryValue)
                }
            };
            
            DayOfWeekSeries = series;
            
            var xAxis = new List<Axis>
            {
                new Axis
                {
                    Labels = dayNames,
                    LabelsRotation = 0,
                    SeparatorsPaint = new SolidColorPaint(SKColors.LightGray)
                    {
                        StrokeThickness = 1
                    }
                }
            };
            
            DayOfWeekXAxes = xAxis;
        }
        
        private void GenerateHourOfDayChart(List<DayTask> tasks)
        {
            var hourlyData = new double[24];
            
            foreach (var task in tasks)
            {
                var start = task.StartHour;
                var end = task.EndHour;
                
                if (start == end)
                {
                    hourlyData[start] += (task.EndMinute - task.StartMinute) / 60.0;
                    continue;
                }
                
                if (task.StartMinute > 0)
                    hourlyData[start] += (60 - task.StartMinute) / 60.0;
                else
                    hourlyData[start] += 1.0;
                
                for (int h = start + 1; h < end; h++)
                {
                    hourlyData[h] += 1.0;
                }
                
                if (task.EndMinute > 0)
                    hourlyData[end] += task.EndMinute / 60.0;
            }
            
            for (int i = 0; i < hourlyData.Length; i++)
            {
                hourlyData[i] *= 60;
            }
            
            var series = new List<ISeries>
            {
                new LineSeries<double>
                {
                    Values = hourlyData,
                    Name = "Распределение по часам",
                    Stroke = new SolidColorPaint(SKColor.Parse("#FF9800"), 3),
                    Fill = new SolidColorPaint(SKColor.Parse("#FFE0B2"), 0.5f),
                    GeometryFill = new SolidColorPaint(SKColor.Parse("#FF9800")),
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#FFFFFF"), 2),
                    GeometrySize = 10
                }
            };
            
            HourOfDaySeries = series;
            
            var hours = Enumerable.Range(0, 24)
                .Select(h => $"{h:D2}:00")
                .ToArray();
                
            var xAxis = new List<Axis>
            {
                new Axis
                {
                    Labels = hours,
                    LabelsRotation = 45,
                    SeparatorsPaint = new SolidColorPaint(SKColors.LightGray)
                    {
                        StrokeThickness = 1
                    }
                }
            };
            
            HourOfDayXAxes = xAxis;
        }
        
        private string FormatTimeSpan(TimeSpan time)
        {
            if (time.TotalDays >= 1)
                return $"{(int)time.TotalDays} д. {time.Hours} ч. {time.Minutes} мин.";
            if (time.TotalHours >= 1)
                return $"{(int)time.TotalHours} ч. {time.Minutes} мин.";
            return $"{time.Minutes} мин.";
        }
        
        private string FormatMinutes(double minutes)
        {
            var time = TimeSpan.FromMinutes(minutes);
            return FormatTimeSpan(time);
        }
    }
    
    public class StatisticItem
    {
        public string Label { get; set; }
        public string Value { get; set; }
    }
    
    public class PieChartData
    {
        public double Value { get; set; }
        public LabelVisual Label { get; set; }
        public SolidColorPaint Fill { get; set; } 
    
        public PieChartData(double value, LabelVisual label, SolidColorPaint fill)
        {
            Value = value;
            Label = label;
            Fill = fill;
        }
    }
    
    public class CategoryInterruption
    {
        public string CategoryName { get; set; }
        public string CategoryColor { get; set; }
        public int Count { get; set; }
        public double AverageTimeToInterruption { get; set; }
        
        public string FormattedAverage => $"{AverageTimeToInterruption:F1} мин.";
    }
}