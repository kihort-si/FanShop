using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;
using FanShop.Services;
using FanShop.View;

namespace FanShop.ViewModels;

public partial class EmployeeViewModel : BaseViewModel
{
    private readonly MainWindowViewModel? _mainWindowViewModel;

    public EmployeeViewModel()
    {
        var (employeesSorted, employeesWithStats) = RefreshEmployees();
        Employees = new ObservableCollection<Employee>(employeesSorted);
        EmployeesWithStats = new ObservableCollection<Employee>(employeesWithStats);
    }

    [ObservableProperty]
    private ObservableCollection<Employee> _employees = new();

    [ObservableProperty]
    private ObservableCollection<Employee> _employeesWithStats = new();

    [ObservableProperty]
    private Employee? _selectedEmployee;

    public EmployeeViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;

        var (employeesSorted, employeesWithStats) = RefreshEmployees();

        Employees = new ObservableCollection<Employee>(employeesSorted);
        EmployeesWithStats = new ObservableCollection<Employee>(employeesWithStats);
    }

    partial void OnSelectedEmployeeChanged(Employee? value)
    {
        RemoveEmployeeCommand.NotifyCanExecuteChanged();
        EditEmployeeCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AddEmployee()
    {
        if (_mainWindowViewModel == null)
            return;

        var editEmployeeViewModel = new EditEmployeeViewModel(_mainWindowViewModel, this);
        var editEmployeeControl = new EditEmployeeControl();
        _mainWindowViewModel.OpenTabRequest(editEmployeeViewModel, editEmployeeControl, "Новый сотрудник");
    }

    [RelayCommand(CanExecute = nameof(CanEditEmployee))]
    private void EditEmployee()
    {
        if (_mainWindowViewModel != null && SelectedEmployee != null)
        {
            var editEmployeeViewModel = new EditEmployeeViewModel(SelectedEmployee, _mainWindowViewModel, this);
            var editEmployeeControl = new EditEmployeeControl();
            _mainWindowViewModel.OpenTabRequest(editEmployeeViewModel, editEmployeeControl,
                $"{SelectedEmployee.FirstName} {SelectedEmployee.Surname}");
        }
    }

    private bool CanEditEmployee => SelectedEmployee != null;

    [RelayCommand(CanExecute = nameof(CanRemoveEmployee))]
    private void RemoveEmployee()
    {
        using var context = new AppDbContext();
        if (SelectedEmployee != null)
        {
            var employee = context.Employees.Find(SelectedEmployee.EmployeeID);
            if (employee != null)
            {
                context.Employees.Remove(employee);
                context.SaveChanges();
                Employees.Remove(SelectedEmployee);
            }
        }
    }

    private bool CanRemoveEmployee => SelectedEmployee != null;

    [RelayCommand]
    private void Cancel()
    {
        _mainWindowViewModel?.CloseTabRequest(this);
    }

    private int GetWorkDaysCount(int employeeId, AppDbContext context)
    {
        var thirtyDaysAgo = DateTime.Now.AddDays(-30);

        try
        {
            return context.WorkDays
                .Where(wd => wd.Date >= thirtyDaysAgo)
                .SelectMany(wd => wd.WorkDayEmployees)
                .Count(wde => wde.EmployeeID == employeeId);
        }
        catch
        {
            return 0;
        }
    }

    public (List<Employee> employeesSorted, List<Employee> employeesWithStats) RefreshEmployees()
    {
        using var context = new AppDbContext();
        List<Employee> allEmployees = context.Employees.ToList();

        List<Employee> employeesSorted = allEmployees.Select(emp => new
            {
                Employee = emp
            })
            .OrderBy(x => x.Employee.Surname)
            .ThenBy(x => x.Employee.FirstName)
            .ThenBy(x => x.Employee.LastName)
            .Select(x => x.Employee)
            .ToList();

        List<Employee> employeesWithStats = allEmployees.Select(emp => new
            {
                Employee = emp,
                WorkDaysCount = GetWorkDaysCount(emp.EmployeeID, context)
            })
            .OrderByDescending(x => x.WorkDaysCount)
            .ThenBy(x => x.Employee.Surname)
            .Select(x => x.Employee)
            .ToList();

        Employees.Clear();
        foreach (var employee in employeesSorted)
            Employees.Add(employee);

        EmployeesWithStats.Clear();
        foreach (var employee in employeesWithStats)
            EmployeesWithStats.Add(employee);

        return (employeesSorted, employeesWithStats);
    }
}
