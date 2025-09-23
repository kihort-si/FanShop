using System.Windows.Input;
using FanShop.Models;
using FanShop.Services;

namespace FanShop.ViewModels;

public class EditEmployeeViewModel : BaseViewModel
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly EmployeeViewModel _employeeViewModel;
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
    
    private Employee? _selectedEmployee;

    public Employee? SelectedEmployee
    {
        get => _selectedEmployee;
        set
        {
            _selectedEmployee = value;
            OnPropertyChanged(nameof(SelectedEmployee));
        }
    }
    
    public ICommand SaveEditedEmployeeCommand { get; }
    public ICommand CancelEditCommand { get; }

    public EditEmployeeViewModel(Employee selectedEmployee, MainWindowViewModel mainWindowViewModel, 
        EmployeeViewModel employeeViewModel)
    {
        SelectedEmployee = selectedEmployee;
        EditEmployee();
        _mainWindowViewModel = mainWindowViewModel;
        _employeeViewModel = employeeViewModel;
        SaveEditedEmployeeCommand = new RelayCommand(SaveEditedEmployee);
        CancelEditCommand = new RelayCommand(Cancel);
    }

    public EditEmployeeViewModel(MainWindowViewModel mainWindowViewModel,
        EmployeeViewModel employeeViewModel)
    {
        AddEmployee();
        _mainWindowViewModel = mainWindowViewModel;
        _employeeViewModel = employeeViewModel;
        SaveEditedEmployeeCommand = new RelayCommand(SaveEditedEmployee);
        CancelEditCommand = new RelayCommand(Cancel);
    }
    
    private void AddEmployee()
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
    }
        
    private void EditEmployee()
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
        }
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
            
            _employeeViewModel.RefreshEmployees();
            _mainWindowViewModel.CloseTabRequest(this);
        }
    }

    private void Cancel(object? parameter)
    {
        _mainWindowViewModel.CloseTabRequest(this);
    }
}