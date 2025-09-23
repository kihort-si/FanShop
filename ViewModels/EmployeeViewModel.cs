using System.Collections.ObjectModel;
using System.Windows.Input;
using FanShop.Models;
using FanShop.Services;
using FanShop.View;

namespace FanShop.ViewModels
{
    public class EmployeeViewModel : BaseViewModel
    {
        private readonly MainWindowViewModel _mainWindowViewModel;
        private ObservableCollection<Employee> _employees = new();
    
        public ObservableCollection<Employee> Employees
        {
            get => _employees;
            set => SetProperty(ref _employees, value);
        }
        
        private ObservableCollection<Employee> _employeesWithStats = new();

        public ObservableCollection<Employee> EmployeesWithStats
        {
            get => _employeesWithStats; 
            set => SetProperty(ref _employeesWithStats, value);
        }
        
        public ICommand AddEmployeeCommand { get; }
        public ICommand EditEmployeeCommand { get; }
        public ICommand RemoveEmployeeCommand { get; }
        public ICommand CloseWindowCommand { get; }
        
        public EmployeeViewModel(MainWindowViewModel mainWindowViewModel)
        {
            _mainWindowViewModel = mainWindowViewModel;
            
            var (employeesSorted, employeesWithStats) = RefreshEmployees();

            Employees = new ObservableCollection<Employee>(employeesSorted);
            EmployeesWithStats = new ObservableCollection<Employee>(employeesWithStats);
            AddEmployeeCommand = new RelayCommand(AddEmployee);
            EditEmployeeCommand = new RelayCommand(EditEmployee, CanEditEmployee);
            RemoveEmployeeCommand = new RelayCommand(RemoveEmployee, CanEditEmployee);
            CloseWindowCommand = new RelayCommand(Cancel);
        }
        
        private void AddEmployee(object? parameter)
        {
            var editEmployeeViewModel = new EditEmployeeViewModel(_mainWindowViewModel, this);
            var editEmployeeControl = new EditEmployeeControl();
            _mainWindowViewModel.OpenTabRequest(editEmployeeViewModel, editEmployeeControl, "Новый сотрудник");
        }

        private void EditEmployee(object? parameter)
        {
            if (SelectedEmployee != null)
            {
                var editEmployeeViewModel = new EditEmployeeViewModel(SelectedEmployee, _mainWindowViewModel, this);
                var editEmployeeControl = new EditEmployeeControl();
                _mainWindowViewModel.OpenTabRequest(editEmployeeViewModel, editEmployeeControl,
                    $"{SelectedEmployee.FirstName} {SelectedEmployee.Surname}");
            }
        }

        private Employee? _selectedEmployee;

        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                _selectedEmployee = value;
                OnPropertyChanged(nameof(SelectedEmployee));
                (RemoveEmployeeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (EditEmployeeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void RemoveEmployee(object? parameter)
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

        private bool CanEditEmployee(object? parameter)
        {
            return SelectedEmployee != null;
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

        private void Cancel(object? parameter)
        {
            _mainWindowViewModel.CloseTabRequest(this);
        }
    }
}