using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;
using FanShop.Services;
using FanShop.View;
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
                            WorkDuration = wde.WorkDuration
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
    private void AddEmployees()
    {
        using var context = new AppDbContext();
        var employeeWindowViewModel = new EmployeeViewModel();

        var availableEmployees = employeeWindowViewModel.Employees
            .Where(e => !Employees.Any(existing => existing.Employee.EmployeeID == e.EmployeeID))
            .ToList();

        employeeWindowViewModel.Employees = new ObservableCollection<Employee>(availableEmployees);

        var selectEmployeeWindow = new SelectEmployeeWindow
        {
            DataContext = employeeWindowViewModel,
            ParentViewModel = this
        };

        SetBlackoutMode(true);

        selectEmployeeWindow.Closed += (s, e) =>
        {
            SetBlackoutMode(false);
        };

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            selectEmployeeWindow.ShowDialog(desktop.MainWindow);
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
    private void PrintPass()
    {
        if (Employees.Count == 0)
        {
            // Show notification - will be handled by view
            return;
        }

        PassDocumentGenerator.CreateWordPass(Date, Employees);
    }

    private bool CanPrintPass => Employees.Count > 0;

    [RelayCommand]
    private void DailySchedule()
    {
        var dayTasksWindow = new DayTasksWindow
        {
            DataContext = new DayTasksWindowViewModel(Date)
        };

        dayTasksWindow.Closed += (s, e) =>
        {
            OnPropertyChanged(nameof(DisplayedTasks));
            OnPropertyChanged(nameof(AdditionalTasksText));
            OnPropertyChanged(nameof(IsAdditionalTasksTextVisible));
        };

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            dayTasksWindow.ShowDialog(desktop.MainWindow);
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

    public void AddEmployeeToDay(Employee employee, string workDuration)
    {
        if (Employees.Any(x => x.Employee.EmployeeID == employee.EmployeeID))
            return;

        Employees.Add(new EmployeeWorkInfo
        {
            Employee = employee,
            WorkDuration = workDuration
        });

        OnPropertyChanged(nameof(Employees));
        OnPropertyChanged(nameof(DisplayedEmployees));
        PrintPassCommand.NotifyCanExecuteChanged();
        NotifyMainControlOfChanges();
    }
}

public class EmployeeWorkInfo
{
    public required Employee Employee { get; set; }
    public required string WorkDuration { get; set; }

    public string FirstName => Employee.FirstName;
    public string Surname => Employee.Surname;
    public string DateOfBirth => Employee.DateOfBirth;
}
