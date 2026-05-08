using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;
using FanShop.Services;

namespace FanShop.ViewModels;

public partial class EditEmployeeViewModel : BaseViewModel
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly EmployeeViewModel _employeeViewModel;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveEditedEmployeeCommand))]
    private string _surname = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveEditedEmployeeCommand))]
    private string _firstName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveEditedEmployeeCommand))]
    private string _lastName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveEditedEmployeeCommand))]
    private string _dateOfBirth = string.Empty;

    [ObservableProperty]
    private DateTime? _dateOfBirthPicker;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveEditedEmployeeCommand))]
    private string _placeOfBirth = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveEditedEmployeeCommand))]
    private string _passport = string.Empty;

    [ObservableProperty]
    private Employee? _editableEmployee;

    [ObservableProperty]
    private Employee? _selectedEmployee;

    public bool CanSaveEmployee =>
        !string.IsNullOrWhiteSpace(Surname) &&
        !string.IsNullOrWhiteSpace(FirstName) &&
        !string.IsNullOrWhiteSpace(LastName) &&
        !string.IsNullOrWhiteSpace(DateOfBirth) &&
        !string.IsNullOrWhiteSpace(PlaceOfBirth) &&
        !string.IsNullOrWhiteSpace(Passport);

    public EditEmployeeViewModel(Employee selectedEmployee, MainWindowViewModel mainWindowViewModel,
        EmployeeViewModel employeeViewModel)
    {
        SelectedEmployee = selectedEmployee;
        EditEmployee();
        _mainWindowViewModel = mainWindowViewModel;
        _employeeViewModel = employeeViewModel;
    }

    public EditEmployeeViewModel(MainWindowViewModel mainWindowViewModel,
        EmployeeViewModel employeeViewModel)
    {
        AddEmployee();
        _mainWindowViewModel = mainWindowViewModel;
        _employeeViewModel = employeeViewModel;
    }

    partial void OnDateOfBirthPickerChanged(DateTime? value)
    {
        if (value.HasValue)
        {
            DateOfBirth = value.Value.ToString("dd.MM.yyyy");
        }
        else
        {
            DateOfBirth = string.Empty;
        }
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

            if (DateTime.TryParseExact(SelectedEmployee.DateOfBirth, "dd.MM.yyyy", null,
                    System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
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

    [RelayCommand(CanExecute = nameof(CanSaveEmployee))]
    private void SaveEditedEmployee()
    {
        try
        {
            using var context = new AppDbContext();
            if (EditableEmployee != null)
            {
                if (SelectedEmployee == null)
                {
                    EditableEmployee.Surname = Surname;
                    EditableEmployee.FirstName = FirstName;
                    EditableEmployee.LastName = LastName;
                    EditableEmployee.DateOfBirth = DateOfBirth;
                    EditableEmployee.PlaceOfBirth = PlaceOfBirth;
                    EditableEmployee.Passport = Passport;

                    context.Employees.Add(EditableEmployee);
                }
                else
                {
                    var employee = context.Employees.Find(SelectedEmployee.EmployeeID);
                    if (employee != null)
                    {
                        employee.FirstName = FirstName;
                        employee.LastName = LastName;
                        employee.Surname = Surname;
                        employee.DateOfBirth = DateOfBirth;
                        employee.PlaceOfBirth = PlaceOfBirth;
                        employee.Passport = Passport;
                        context.Employees.Update(employee);
                    }
                }

                context.SaveChanges();

                _employeeViewModel.RefreshEmployees();
                _mainWindowViewModel.CloseTabRequest(this);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SaveEditedEmployee] FAILED: {ex}");
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _mainWindowViewModel.CloseTabRequest(this);
    }
}
