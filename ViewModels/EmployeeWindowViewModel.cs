using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using FanShop.Models;
using FanShop.Services;

namespace FanShop.ViewModels
{
    public class EmployeeWindowViewModel : BaseViewModel
    {
        private ObservableCollection<Employee> _employees = new();
    
        public ObservableCollection<Employee> Employees
        {
            get => _employees;
            set => SetProperty(ref _employees, value);
        }
        
        public ICommand AddEmployeeCommand { get; }
        public ICommand EditEmployeeCommand { get; }
        public ICommand RemoveEmployeeCommand { get; }
        public ICommand CloseWindowCommand { get; }
        public ICommand SaveEditedEmployeeCommand { get; }
        public ICommand CancelEditCommand { get; }

        private bool _isEditOverlayVisible;

        public bool IsEditOverlayVisible
        {
            get => _isEditOverlayVisible;
            set => SetProperty(ref _isEditOverlayVisible, value);
        }

        private Employee? _editableEmployee;

        public Employee? EditableEmployee
        {
            get => _editableEmployee;
            set
            {
                if (SetProperty(ref _editableEmployee, value))
                {
                    OnPropertyChanged(nameof(CanSaveEmployee));
                }
            }
        }
        
        private string _surname = string.Empty;
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        private string _dateOfBirth = string.Empty;
        private string _placeOfBirth = string.Empty;
        private string _passport = string.Empty;
        
        public string Surname
        {
            get => _surname;
            set
            {
                _surname = value;
                if (EditableEmployee != null)
                    EditableEmployee.Surname = value;
                OnPropertyChanged(nameof(Surname));
                OnPropertyChanged(nameof(CanSaveEmployee));
            }
        }
        
        public string FirstName
        {
            get => _firstName;
            set
            {
                _firstName = value;
                if (EditableEmployee != null)
                    EditableEmployee.FirstName = value;
                OnPropertyChanged(nameof(FirstName));
                OnPropertyChanged(nameof(CanSaveEmployee));
            }
        }
        
        public string LastName
        {
            get => _lastName;
            set
            {
                _lastName = value;
                if (EditableEmployee != null)
                    EditableEmployee.LastName = value;
                OnPropertyChanged(nameof(LastName));
                OnPropertyChanged(nameof(CanSaveEmployee));
            }
        }
        
        public string DateOfBirth
        {
            get => _dateOfBirth;
            set
            {
                _dateOfBirth = value;
                if (EditableEmployee != null)
                    EditableEmployee.DateOfBirth = value;
                OnPropertyChanged(nameof(DateOfBirth));
                OnPropertyChanged(nameof(CanSaveEmployee));
            }
        }
        
        public string PlaceOfBirth
        {
            get => _placeOfBirth;
            set
            {
                _placeOfBirth = value;
                if (EditableEmployee != null)
                    EditableEmployee.PlaceOfBirth = value;
                OnPropertyChanged(nameof(PlaceOfBirth));
                OnPropertyChanged(nameof(CanSaveEmployee));
            }
        }
        
        public string Passport
        {
            get => _passport;
            set
            {
                _passport = value;
                if (EditableEmployee != null)
                    EditableEmployee.Passport = value;
                OnPropertyChanged(nameof(Passport));
                OnPropertyChanged(nameof(CanSaveEmployee));
            }
        }
        
        public bool CanSaveEmployee
        {
            get
            {
                return !string.IsNullOrWhiteSpace(_surname) &&
                       !string.IsNullOrWhiteSpace(_firstName) &&
                       !string.IsNullOrWhiteSpace(_lastName) &&
                       !string.IsNullOrWhiteSpace(_dateOfBirth) &&
                       !string.IsNullOrWhiteSpace(_placeOfBirth) &&
                       !string.IsNullOrWhiteSpace(_passport);
            }
        }

        public EmployeeWindowViewModel()
        {
            using var context = new AppDbContext();
            var allEmployees = context.Employees.ToList();
        
            var employeesWithStats = allEmployees.Select(emp => new
                {
                    Employee = emp,
                    WorkDaysCount = GetWorkDaysCount(emp.EmployeeID, context)
                })
                .OrderByDescending(x => x.WorkDaysCount)
                .ThenBy(x => x.Employee.Surname)
                .Select(x => x.Employee)
                .ToList();

            Employees = new ObservableCollection<Employee>(employeesWithStats);
            AddEmployeeCommand = new RelayCommand(AddEmployee);
            EditEmployeeCommand = new RelayCommand(EditEmployee, CanEditEmployee);
            RemoveEmployeeCommand = new RelayCommand(RemoveEmployee, CanEditEmployee);
            CloseWindowCommand = new RelayCommand(CloseWindow);
            SaveEditedEmployeeCommand = new RelayCommand(SaveEditedEmployee);
            CancelEditCommand = new RelayCommand(CancelEdit);
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

        private void AddEmployee(object? parameter)
        {
            SelectedEmployee = null;
            EditableEmployee = new Employee();
            
            Surname = string.Empty;
            FirstName = string.Empty;
            LastName = string.Empty;
            DateOfBirth = string.Empty;
            DateOfBirthPicker = null;
            PlaceOfBirth = string.Empty;
            Passport = string.Empty;
        
            IsEditOverlayVisible = true;
        }
        
        private void EditEmployee(object? parameter)
        {
            if (SelectedEmployee != null)
            {
                EditableEmployee = new Employee();
                
                Surname = SelectedEmployee.Surname;
                FirstName = SelectedEmployee.FirstName;
                LastName = SelectedEmployee.LastName;
                DateOfBirth = SelectedEmployee.DateOfBirth;
                
                if (DateTime.TryParseExact(SelectedEmployee.DateOfBirth, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    DateOfBirthPicker = parsedDate;
                }
                else
                {
                    DateOfBirthPicker = null;
                }
                
                PlaceOfBirth = SelectedEmployee.PlaceOfBirth;
                Passport = SelectedEmployee.Passport;
                
                IsEditOverlayVisible = true;
            }
        }

        private void SaveEditedEmployee(object? parameter)
        {
            using var context = new AppDbContext();
            if (EditableEmployee != null)
            {
                if (SelectedEmployee == null)
                {
                    context.Employees.Add(EditableEmployee);
                    context.SaveChanges();
                }
                else
                {
                    var employee = context.Employees.Find(SelectedEmployee.EmployeeID);
                    if (employee != null)
                    {
                        employee.FirstName = EditableEmployee.FirstName;
                        employee.LastName = EditableEmployee.LastName;
                        employee.Surname = EditableEmployee.Surname;
                        employee.DateOfBirth = EditableEmployee.DateOfBirth;
                        employee.PlaceOfBirth = EditableEmployee.PlaceOfBirth;
                        employee.Passport = EditableEmployee.Passport;
                        context.Employees.Update(employee);
                        context.SaveChanges();
                    }
                }

                context.SaveChanges();
                IsEditOverlayVisible = false;
            
                RefreshEmployeesSorting();
            }
        }

        private void CancelEdit(object? parameter)
        {
            IsEditOverlayVisible = false;
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
        
        private DateTime? _dateOfBirthPicker;
        
        public DateTime? DateOfBirthPicker
        {
            get => _dateOfBirthPicker;
            set
            {
                _dateOfBirthPicker = value;
                if (value.HasValue)
                {
                    DateOfBirth = value.Value.ToString("dd.MM.yyyy");
                }
                else
                {
                    DateOfBirth = string.Empty;
                }
                OnPropertyChanged(nameof(DateOfBirthPicker));
            }
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

        private void RefreshEmployeesSorting()
        {
            using var context = new AppDbContext();
            var allEmployees = context.Employees.ToList();
        
            var employeesWithStats = allEmployees.Select(emp => new
                {
                    Employee = emp,
                    WorkDaysCount = GetWorkDaysCount(emp.EmployeeID, context)
                })
                .OrderByDescending(x => x.WorkDaysCount)
                .ThenBy(x => x.Employee.Surname)
                .Select(x => x.Employee)
                .ToList();

            Employees.Clear();
            foreach (var employee in employeesWithStats)
            {
                Employees.Add(employee);
            }
        }

        private void CloseWindow(object? parameter)
        {
            Application.Current.Windows[1]?.Close();
        }
    }
}