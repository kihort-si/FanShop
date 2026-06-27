using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
                    .Include(w => w.WorkDayEmployees)
                    .ThenInclude(wde => wde.Employee)
                    .FirstOrDefault(w => w.Date == Date);

                _employees = workDay != null
                    ? new ObservableCollection<EmployeeWorkInfo>(
                        workDay.WorkDayEmployees.Select(wde => new EmployeeWorkInfo
                        {
                            Employee = wde.Employee,
                            WorkDuration = wde.WorkDuration,
                            WorkDayEmployeeID = wde.WorkDayEmployeeID,
                            IncludeInPass = wde.IncludeInPass,
                            IncludeInSalary = wde.IncludeInSalary,
                            StatisticsChangedCallback = NotifyMainControlOfChanges
                        }))
                    : new ObservableCollection<EmployeeWorkInfo>();
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

    [ObservableProperty]
    private EmployeeWorkInfo? _selectedEmployee;

    [ObservableProperty]
    private MatchInfo? _match;

    [ObservableProperty]
    private bool _hasMatch;

    public bool ShowChangeNotice => HasMatch && Match != null && Match.CanChange;

    [ObservableProperty]
    private bool _isCurrentMonth;

    [ObservableProperty]
    private bool _isBlackoutMode;

    [ObservableProperty]
    private bool _isEmployeeView;

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
        if (!HasMatch)
        {
            var dayDetailsWindow = new DayDetailsWindow
            {
                DataContext = this
            };

            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                dayDetailsWindow.ShowDialog(desktop.MainWindow);
            }
            else
            {
                dayDetailsWindow.Show();
            }

            SetBlackoutMode(true);

            dayDetailsWindow.Closed += (s, e) =>
            {
                SetBlackoutMode(false);
            };
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
                .Include(w => w.WorkDayEmployees)
                .FirstOrDefault(w => w.Date == Date);

            if (workDay != null)
            {
                var workDayEmployeeToRemove = workDay.WorkDayEmployees
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

    [ObservableProperty]
    private string _additionalEmployeesText = string.Empty;

    [ObservableProperty]
    private bool _isAdditionalEmployeesTextVisible;

    public IEnumerable<object> DisplayedEmployees
    {
        get
        {
            var displayed = Employees.Take(4).Select(e => e.FirstName).ToList();

            if (Employees.Count > 4)
            {
                AdditionalEmployeesText = $"Ещё {Employees.Count - 4} {GetEmployeesTextForm(Employees.Count - 4)}";
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

    private string GetEmployeesTextForm(int count)
    {
        if (count % 10 == 1 && count % 100 != 11)
            return "сотрудник";
        else if ((count % 10 >= 2 && count % 10 <= 4) && (count % 100 < 10 || count % 100 >= 20))
            return "сотрудника";
        else
            return "сотрудников";
    }

    [ObservableProperty]
    private string _additionalTasksText = string.Empty;

    [ObservableProperty]
    private bool _isAdditionalTasksTextVisible;

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

    public void AddEmployeeToDay(Employee employee, string workDuration, int workDayEmployeeId)
    {
        if (Employees.Any(x => x.Employee.EmployeeID == employee.EmployeeID))
            return;

        Employees.Add(new EmployeeWorkInfo
        {
            Employee = employee,
            WorkDuration = workDuration,
            WorkDayEmployeeID = workDayEmployeeId,
            StatisticsChangedCallback = NotifyMainControlOfChanges
        });

        OnPropertyChanged(nameof(Employees));
        OnPropertyChanged(nameof(DisplayedEmployees));
        PrintPassCommand.NotifyCanExecuteChanged();
        NotifyMainControlOfChanges();
    }
}

public partial class EmployeeWorkInfo : ObservableObject
{
    public required Employee Employee { get; set; }
    public required string WorkDuration { get; set; }

    public int WorkDayEmployeeID { get; set; }

    [ObservableProperty]
    private bool _includeInPass = true;

    [ObservableProperty]
    private bool _includeInSalary = true;

    public Action? StatisticsChangedCallback { get; set; }

    public string FirstName => Employee.FirstName;
    public string Surname => Employee.Surname;
    public string DateOfBirth => Employee.DateOfBirth;

    partial void OnIncludeInPassChanged(bool value) => PersistFlag(wde => wde.IncludeInPass = value);
    partial void OnIncludeInSalaryChanged(bool value)
    {
        PersistFlag(wde => wde.IncludeInSalary = value);
        StatisticsChangedCallback?.Invoke();
    }

    private void PersistFlag(Action<WorkDayEmployee> mutate)
    {
        if (WorkDayEmployeeID == 0) return;
        using var context = new AppDbContext();
        var wde = context.WorkDayEmployees.Find(WorkDayEmployeeID);
        if (wde == null) return;
        mutate(wde);
        context.SaveChanges();
    }
}
