using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;
using FanShop.Services;
using FanShop.Windows;
using Microsoft.EntityFrameworkCore;

namespace FanShop.ViewModels;

public partial class CalendarDayViewModel : BaseViewModel
{
    public DateTime Date { get; set; }

    private ObservableCollection<EmployeeWorkInfo>? _employees;

    public ObservableCollection<EmployeeWorkInfo> Employees
    {
        get
        {
            if (_employees == null)
            {
                using var context = new AppDbContext();
                var workDay = context.WorkDays
                    .Include(w => w.WorkDayEmployee)
                    .ThenInclude(wde => wde.Employee)
                    .Include(w => w.WorkDayEmployee)
                    .ThenInclude(wde => wde.Position)
                    .ThenInclude(position => position.Shop)
                    .FirstOrDefault(w => w.Date == Date);

                _employees = new ObservableCollection<EmployeeWorkInfo>();
                if (workDay != null)
                {
                    var shops = LoadWorkplaceShops(context);
                    foreach (var wde in workDay.WorkDayEmployee)
                    {
                        var info = new EmployeeWorkInfo
                        {
                            Employee = wde.Employee,
                            WorkDuration = wde.WorkDuration,
                            WorkDayEmployeeID = wde.WorkDayEmployeeID,
                            IncludeInPass = wde.IncludeInPass,
                            IncludeInSalary = wde.IncludeInSalary,
                            StatisticsChangedCallback = NotifyMainControlOfChanges,
                            WorkplaceChangedCallback = NotifyEmployeeGroupsChanged
                        };
                        info.InitializeWorkplace(shops, wde.PositionID);
                        _employees.Add(info);
                    }
                }
            }

            return _employees;
        }
        set => SetProperty(ref _employees, value);
    }

    private ObservableCollection<DayTask>? _tasks;

    public ObservableCollection<DayTask> Tasks
    {
        get
        {
            using var context = new AppDbContext();
            var tasks = context.DayTasks
                .Where(t => t.Date == Date)
                .OrderBy(t => t.StartHour)
                .ThenBy(t => t.StartMinute);

            _tasks = new ObservableCollection<DayTask>(tasks);
            return _tasks;
        }
        set => SetProperty(ref _tasks, value);
    }

    [ObservableProperty] private EmployeeWorkInfo? _selectedEmployee;

    [ObservableProperty] private MatchInfo? _match;

    [ObservableProperty] private bool _hasMatch;

    public bool ShowChangeNotice => HasMatch && Match != null && Match.CanChange;

    [ObservableProperty] private bool _isCurrentMonth;

    [ObservableProperty] private bool _isBlackoutMode;

    [ObservableProperty] private bool _isEmployeeView;

    private int? _employeeShopFilterId;

    public MainViewModel? MainViewModel { get; set; }

    public CalendarDayViewModel()
    {
        IsEmployeeView = true;
    }

    partial void OnMatchChanged(MatchInfo? value)
    {
        HasMatch = value != null;
        OnPropertyChanged(nameof(ShowChangeNotice));
    }

    partial void OnSelectedEmployeeChanged(EmployeeWorkInfo? value)
    {
        RemoveEmployeesCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ShowDayDetails()
    {
        if (MainViewModel?.TryAddMultiShift(this) == true)
            return;

        if (!HasMatch)
        {
            var dayDetailsWindow = new DayDetailsWindow
            {
                DataContext = this
            };

            if (Avalonia.Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                dayDetailsWindow.ShowDialog(desktop.MainWindow);
            }
            else
            {
                dayDetailsWindow.Show();
            }

            SetBlackoutMode(true);

            dayDetailsWindow.Closed += (s, e) => { SetBlackoutMode(false); };
        }
    }

    [RelayCommand]
    private async Task AddEmployees()
    {
        using var context = new AppDbContext();
        var employeeWindowViewModel = new EmployeeViewModel();

        var availableEmployees = employeeWindowViewModel.EmployeesWithStats
            .Where(e => !Employees.Any(existing => existing.Employee.EmployeeID == e.EmployeeID))
            .ToList();

        employeeWindowViewModel.Employees = new ObservableCollection<Employee>(availableEmployees);

        var selectEmployeeWindow = new SelectEmployeeWindow
        {
            DataContext = employeeWindowViewModel,
            ParentViewModel = this
        };

        var owner = GetCurrentDayDetailsOwner();
        if (owner != null)
        {
            await selectEmployeeWindow.ShowDialog(owner);
        }
        else
        {
            selectEmployeeWindow.Show();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveEmployees))]
    private void RemoveEmployees()
    {
        if (SelectedEmployee != null)
        {
            using var context = new AppDbContext();

            var workDay = context.WorkDays
                .Include(w => w.WorkDayEmployee)
                .FirstOrDefault(w => w.Date == Date);

            if (workDay != null)
            {
                var workDayEmployeeToRemove = workDay.WorkDayEmployee
                    .FirstOrDefault(wde => wde.EmployeeID == SelectedEmployee.Employee.EmployeeID);

                if (workDayEmployeeToRemove != null)
                {
                    context.Remove(workDayEmployeeToRemove);
                    context.SaveChanges();
                }
            }

            Employees.Remove(SelectedEmployee);
            SelectedEmployee = null;

            NotifyMainControlOfChanges();

            OnPropertyChanged(nameof(Employees));
            OnPropertyChanged(nameof(EmployeeGroups));
            OnPropertyChanged(nameof(DisplayedEmployees));
            PrintPassCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRemoveEmployees => SelectedEmployee != null;

    [RelayCommand(CanExecute = nameof(CanPrintPass))]
    private async Task PrintPass()
    {
        if (Employees.Count == 0)
        {
            return;
        }

        var owner = GetCurrentDayDetailsOwner();
        if (!await PassTemplateService.EnsureTemplateAsync(owner))
        {
            return;
        }

        PassDocumentGenerator.CreateWordPass(Date, Employees);
    }

    private bool CanPrintPass => Employees.Count > 0;

    [RelayCommand]
    private async void DailySchedule()
    {
        var dayTasksWindow = new DayTasksWindow
        {
            DataContext = new DayTasksWindowViewModel(Date),
            ParentViewModel = this
        };

        dayTasksWindow.Closed += (s, e) =>
        {
            OnPropertyChanged(nameof(DisplayedTasks));
            OnPropertyChanged(nameof(AdditionalTasksText));
            OnPropertyChanged(nameof(IsAdditionalTasksTextVisible));
        };

        var owner = GetCurrentDayDetailsOwner();
        if (owner != null)
        {
            await dayTasksWindow.ShowDialog(owner);
        }
        else
        {
            dayTasksWindow.Show();
        }
    }

    [RelayCommand]
    private void CloseWindow()
    {
        SetBlackoutMode(false);
    }

    public void SetBlackoutMode(bool isBlackout)
    {
        IsBlackoutMode = isBlackout;
    }

    private Window? GetCurrentDayDetailsOwner()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        return desktop.Windows
                   .OfType<DayDetailsWindow>()
                   .LastOrDefault(window => ReferenceEquals(window.DataContext, this))
               ?? desktop.MainWindow;
    }

    [ObservableProperty] private string _additionalEmployeesText = string.Empty;

    [ObservableProperty] private bool _isAdditionalEmployeesTextVisible;

    public IEnumerable<object> DisplayedEmployees
    {
        get
        {
            var filtered = Employees
                .Where(employee => _employeeShopFilterId == null || employee.SelectedShop?.ShopID == _employeeShopFilterId)
                .ToList();
            var displayed = filtered.Take(4).Select(employee => new EmployeeCalendarChip
            {
                Name = employee.FirstName,
                Background = GetShopChipBackground(employee.SelectedShop),
                Foreground = GetShopChipForeground(employee.SelectedShop)
            }).ToList();

            if (filtered.Count > 4)
            {
                AdditionalEmployeesText = $"Ещё {filtered.Count - 4} {GetEmployeesTextForm(filtered.Count - 4)}";
                IsAdditionalEmployeesTextVisible = true;
            }
            else
            {
                AdditionalEmployeesText = string.Empty;
                IsAdditionalEmployeesTextVisible = false;
            }

            return displayed;
        }
    }

    public void SetEmployeeShopFilter(int? shopId)
    {
        _employeeShopFilterId = shopId;
        OnPropertyChanged(nameof(DisplayedEmployees));
    }

    public void RefreshWorkplaceCatalog()
    {
        if (_employees == null)
            return;

        using var context = new AppDbContext();
        var shops = LoadWorkplaceShops(context);
        foreach (var employee in _employees)
            employee.InitializeWorkplace(shops, employee.PositionID);

        OnPropertyChanged(nameof(EmployeeGroups));
        OnPropertyChanged(nameof(DisplayedEmployees));
    }

    private static IBrush GetShopChipBackground(Shop? shop)
    {
        if (shop?.IsDefault != false)
            return Brush.Parse("#D9F1FB");

        string[] palette = ["#C9E3F7", "#BCD8F2", "#D2E5FA", "#C5DCF0"];
        return Brush.Parse(palette[Math.Abs(shop.ShopID) % palette.Length]);
    }

    private static IBrush GetShopChipForeground(Shop? shop) =>
        shop?.IsDefault == false ? Brush.Parse("#174F7A") : Brushes.MidnightBlue;

    private string GetEmployeesTextForm(int count)
    {
        if (count % 10 == 1 && count % 100 != 11)
            return "сотрудник";
        else if ((count % 10 >= 2 && count % 10 <= 4) && (count % 100 < 10 || count % 100 >= 20))
            return "сотрудника";
        else
            return "сотрудников";
    }

    [ObservableProperty] private string _additionalTasksText = string.Empty;

    [ObservableProperty] private bool _isAdditionalTasksTextVisible;

    public IEnumerable<object> DisplayedTasks
    {
        get
        {
            var displayed = Tasks.Take(4).Select(t => t.Title).ToList();

            if (Tasks.Count > 4)
            {
                AdditionalTasksText = $"+{Tasks.Count - 4} {GetTasksTextForm(Tasks.Count - 4)}";
                IsAdditionalTasksTextVisible = true;
            }
            else
            {
                AdditionalTasksText = string.Empty;
                IsAdditionalTasksTextVisible = false;
            }

            return displayed;
        }
    }

    private string GetTasksTextForm(int count)
    {
        if (count % 10 == 1 && count % 100 != 11)
            return "задача";
        else if ((count % 10 >= 2 && count % 10 <= 4) && (count % 100 < 10 || count % 100 >= 20))
            return "задачи";
        else
            return "задач";
    }

    private void NotifyMainControlOfChanges()
    {
        MainViewModel?.RefreshStatistics();
    }

    public IEnumerable<ShopEmployeeGroup> EmployeeGroups => Employees
        .GroupBy(employee => new
        {
            ShopID = employee.SelectedShop?.ShopID ?? 0,
            ShopName = employee.SelectedShop?.ShopName ?? "Магазин не выбран"
        })
        .OrderByDescending(group => group.Any(employee => employee.SelectedShop?.IsDefault == true))
        .ThenBy(group => group.Key.ShopName)
        .Select(group => new ShopEmployeeGroup
        {
            ShopName = group.Key.ShopName,
            Positions = group
                .GroupBy(employee => new
                {
                    PositionID = employee.SelectedPosition?.PositionID ?? 0,
                    PositionName = employee.SelectedPosition?.PositionName ?? "Должность не выбрана"
                })
                .OrderBy(positionGroup => positionGroup.Count())
                .ThenBy(positionGroup => positionGroup.Key.PositionName)
                .Select(positionGroup => new PositionEmployeeGroup
                {
                    PositionName = positionGroup.Key.PositionName,
                    Employees = positionGroup
                        .OrderBy(employee => employee.Surname)
                        .ThenBy(employee => employee.FirstName)
                        .ToList()
                })
                .ToList()
        })
        .ToList();

    private static List<Shop> LoadWorkplaceShops(AppDbContext context) => context.Shops
        .AsNoTracking()
        .Include(shop => shop.Positions)
        .Where(shop => shop.Positions.Any())
        .OrderByDescending(shop => shop.IsDefault)
        .ThenBy(shop => shop.ShopName)
        .ToList();

    private void NotifyEmployeeGroupsChanged()
    {
        OnPropertyChanged(nameof(EmployeeGroups));
        OnPropertyChanged(nameof(DisplayedEmployees));
        NotifyMainControlOfChanges();
    }

    public void AddEmployeeToDay(
        Employee employee,
        string workDuration,
        int workDayEmployeeId,
        int positionId,
        string positionName)
    {
        if (Employees.Any(x => x.Employee.EmployeeID == employee.EmployeeID))
            return;

        using var context = new AppDbContext();
        var info = new EmployeeWorkInfo
        {
            Employee = employee,
            WorkDuration = workDuration,
            WorkDayEmployeeID = workDayEmployeeId,
            PositionID = positionId,
            PositionName = positionName,
            StatisticsChangedCallback = NotifyMainControlOfChanges,
            WorkplaceChangedCallback = NotifyEmployeeGroupsChanged
        };
        info.InitializeWorkplace(LoadWorkplaceShops(context), positionId);
        Employees.Add(info);

        OnPropertyChanged(nameof(Employees));
        OnPropertyChanged(nameof(EmployeeGroups));
        OnPropertyChanged(nameof(DisplayedEmployees));

        PrintPassCommand.NotifyCanExecuteChanged();

        NotifyMainControlOfChanges();
    }
}

public partial class EmployeeWorkInfo : ObservableObject
{
    public required Employee Employee { get; set; }

    [ObservableProperty] private string _workDuration = "Целый день";

    public IReadOnlyList<string> WorkDurations { get; } =
    [
        "Целый день",
        "Полдня"
    ];

    public int PositionID { get; set; }

    [ObservableProperty] private string _positionName = string.Empty;

    public ObservableCollection<Shop> AvailableShops { get; } = new();
    public ObservableCollection<Position> AvailablePositions { get; } = new();

    public bool HasMultipleShops => AvailableShops.Count > 1;
    public bool HasMultiplePositions => AvailablePositions.Count > 1;

    [ObservableProperty] private Shop? _selectedShop;
    [ObservableProperty] private Position? _selectedPosition;

    private bool _isInitializingWorkplace;

    public int WorkDayEmployeeID { get; set; }

    [ObservableProperty] private bool _includeInPass = true;

    [ObservableProperty] private bool _includeInSalary = true;

    public Action? StatisticsChangedCallback { get; set; }
    public Action? WorkplaceChangedCallback { get; set; }

    public string FirstName => Employee.FirstName;
    public string Surname => Employee.Surname;
    public string DateOfBirth => Employee.DateOfBirth;

    partial void OnIncludeInPassChanged(bool value) => PersistFlag(wde => wde.IncludeInPass = value);

    partial void OnIncludeInSalaryChanged(bool value)
    {
        PersistFlag(wde => wde.IncludeInSalary = value);
        StatisticsChangedCallback?.Invoke();
    }

    partial void OnWorkDurationChanged(string value)
    {
        if (WorkDayEmployeeID == 0)
            return;

        using var context = new AppDbContext();

        var wde = context.WorkDayEmployee.Find(WorkDayEmployeeID);

        if (wde == null)
            return;

        wde.WorkDuration = value;

        var salaryService = new SalaryService(context);

        var workDate = context.WorkDays
            .Where(x => x.WorkDayID == wde.WorkDayID)
            .Select(x => x.Date)
            .First();

        wde.SalaryAtMoment = salaryService.GetSalaryForShift(
            wde.PositionID,
            workDate,
            value);

        context.SaveChanges();

        StatisticsChangedCallback?.Invoke();
    }

    public void InitializeWorkplace(IEnumerable<Shop> shops, int positionId)
    {
        _isInitializingWorkplace = true;
        AvailableShops.Clear();
        foreach (var shop in shops)
            AvailableShops.Add(shop);
        OnPropertyChanged(nameof(HasMultipleShops));

        var position = AvailableShops.SelectMany(shop => shop.Positions)
            .FirstOrDefault(item => item.PositionID == positionId);
        SelectedShop = AvailableShops.FirstOrDefault(shop => shop.ShopID == position?.ShopID);
        RefreshAvailablePositions();
        SelectedPosition = AvailablePositions.FirstOrDefault(item => item.PositionID == positionId);
        PositionID = SelectedPosition?.PositionID ?? positionId;
        PositionName = SelectedPosition?.PositionName ?? PositionName;
        _isInitializingWorkplace = false;
    }

    partial void OnSelectedShopChanged(Shop? value)
    {
        RefreshAvailablePositions();
        if (_isInitializingWorkplace)
            return;

        SelectedPosition = AvailablePositions.FirstOrDefault(position => position.IsDefault)
                           ?? AvailablePositions.FirstOrDefault();
    }

    partial void OnSelectedPositionChanged(Position? value)
    {
        if (_isInitializingWorkplace || value == null || WorkDayEmployeeID == 0)
            return;

        using var context = new AppDbContext();
        var wde = context.WorkDayEmployee.Find(WorkDayEmployeeID);
        if (wde == null)
            return;

        wde.PositionID = value.PositionID;
        var workDate = context.WorkDays
            .Where(day => day.WorkDayID == wde.WorkDayID)
            .Select(day => day.Date)
            .First();
        wde.SalaryAtMoment = new SalaryService(context).GetSalaryForShift(
            value.PositionID, workDate, wde.WorkDuration);
        context.SaveChanges();

        PositionID = value.PositionID;
        PositionName = value.PositionName;
        WorkplaceChangedCallback?.Invoke();
    }

    private void RefreshAvailablePositions()
    {
        AvailablePositions.Clear();
        if (SelectedShop == null)
        {
            OnPropertyChanged(nameof(HasMultiplePositions));
            return;
        }

        foreach (var position in SelectedShop.Positions
                     .OrderByDescending(item => item.IsDefault)
                     .ThenBy(item => item.PositionName))
            AvailablePositions.Add(position);
        OnPropertyChanged(nameof(HasMultiplePositions));
    }

    private void PersistFlag(Action<WorkDayEmployee> mutate)
    {
        if (WorkDayEmployeeID == 0) return;
        using var context = new AppDbContext();
        var wde = context.WorkDayEmployee.Find(WorkDayEmployeeID);
        if (wde == null) return;
        mutate(wde);
        context.SaveChanges();
    }
}

public sealed class ShopEmployeeGroup
{
    public string ShopName { get; init; } = string.Empty;
    public IReadOnlyList<PositionEmployeeGroup> Positions { get; init; } = [];
}

public sealed class PositionEmployeeGroup
{
    public string PositionName { get; init; } = string.Empty;
    public IReadOnlyList<EmployeeWorkInfo> Employees { get; init; } = [];
    public double TableHeight => 42 + Employees.Count * 36;
}

public sealed class EmployeeCalendarChip
{
    public string Name { get; init; } = string.Empty;
    public IBrush Background { get; init; } = Brushes.Transparent;
    public IBrush Foreground { get; init; } = Brushes.MidnightBlue;
}
