using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FanShop.Models;

namespace FanShop.ViewModels
{
    public class EmployeeWindowViewModel : BaseViewModel
    {
        public ObservableCollection<Employee> Employees { get; set; } = new();

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
            set
            {
                _isEditOverlayVisible = value;
                OnPropertyChanged(nameof(IsEditOverlayVisible));
            }
        }

        private Employee? _editableEmployee;
        public Employee? EditableEmployee
        {
            get => _editableEmployee;
            set
            {
                _editableEmployee = value;
                OnPropertyChanged(nameof(EditableEmployee));
            }
        }
        
        public EmployeeWindowViewModel()
        {
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
                (EditEmployeeCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Добавлено
            }
        }

        private void AddEmployee(object? parameter)
        {
            EditableEmployee = new Employee
            {
                FirstName = string.Empty,
                LastName = string.Empty,
                Surname = string.Empty,
                DateOfBirth = string.Empty,
                PlaceOfBirth = string.Empty,
                Passport = string.Empty
            };

            IsEditOverlayVisible = true;
        }

        private void EditEmployee(object? parameter)
        {
            if (SelectedEmployee != null)
            {
                EditableEmployee = new Employee
                {
                    FirstName = SelectedEmployee.FirstName,
                    LastName = SelectedEmployee.LastName,
                    Surname = SelectedEmployee.Surname,
                    DateOfBirth = SelectedEmployee.DateOfBirth,
                    PlaceOfBirth = SelectedEmployee.PlaceOfBirth,
                    Passport = SelectedEmployee.Passport
                };
                IsEditOverlayVisible = true;
            }
        }
        
        private void SaveEditedEmployee(object? parameter)
        {
            if (EditableEmployee != null)
            {
                if (SelectedEmployee == null)
                {
                    Employees.Add(new Employee
                    {
                        FirstName = EditableEmployee.FirstName,
                        LastName = EditableEmployee.LastName,
                        Surname = EditableEmployee.Surname,
                        DateOfBirth = EditableEmployee.DateOfBirth,
                        PlaceOfBirth = EditableEmployee.PlaceOfBirth,
                        Passport = EditableEmployee.Passport
                    });
                }
                else
                {
                    var index = Employees.IndexOf(SelectedEmployee);
                    if (index >= 0)
                    {
                        Employees[index] = new Employee
                        {
                            FirstName = EditableEmployee.FirstName,
                            LastName = EditableEmployee.LastName,
                            Surname = EditableEmployee.Surname,
                            DateOfBirth = EditableEmployee.DateOfBirth,
                            PlaceOfBirth = EditableEmployee.PlaceOfBirth,
                            Passport = EditableEmployee.Passport
                        };
                    }

                    SelectedEmployee = Employees[index];
                }

                IsEditOverlayVisible = false;
            }
        }
        
        private void CancelEdit(object? parameter)
        {
            IsEditOverlayVisible = false;
        }

        private void RemoveEmployee(object? parameter)
        {
            if (SelectedEmployee != null)
            {
                Employees.Remove(SelectedEmployee);
            }
        }

        private bool CanEditEmployee(object? parameter)
        {
            return SelectedEmployee != null;
        }

        private void CloseWindow(object? parameter)
        {
            Application.Current.Windows[1]?.Close();
        }
    }
}