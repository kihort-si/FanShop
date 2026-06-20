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
    private static readonly EmployeeNameComparer NameComparer = new();

    public EmployeeViewModel()
    {
        RefreshEmployees();
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

        RefreshEmployees();
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

        List<Employee> employeesSorted = allEmployees
            .OrderBy(employee => employee, NameComparer)
            .ToList();

        List<Employee> employeesWithStats = allEmployees.Select(emp => new
            {
                Employee = emp,
                WorkDaysCount = GetWorkDaysCount(emp.EmployeeID, context)
            })
            .OrderByDescending(x => x.WorkDaysCount)
            .ThenBy(x => x.Employee, NameComparer)
            .Select(x => x.Employee)
            .ToList();

        Employees = new ObservableCollection<Employee>(employeesSorted);
        EmployeesWithStats = new ObservableCollection<Employee>(employeesWithStats);

        return (employeesSorted, employeesWithStats);
    }

    public void SelectEmployeeById(int employeeId)
    {
        if (employeeId == 0)
        {
            return;
        }

        SelectedEmployee = Employees.FirstOrDefault(employee => employee.EmployeeID == employeeId);
    }

    private sealed class EmployeeNameComparer : IComparer<Employee>
    {
        public int Compare(Employee? x, Employee? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            return StringComparer.Ordinal.Compare(GetSortKey(x), GetSortKey(y));
        }

        private static string GetSortKey(Employee employee)
        {
            return $"{Normalize(employee.Surname)}\u0000{Normalize(employee.FirstName)}\u0000{Normalize(employee.LastName)}";
        }

        private static string Normalize(string? value) =>
            (value ?? string.Empty).Trim().ToUpperInvariant().Replace('Ё', 'Е');
    }
}
