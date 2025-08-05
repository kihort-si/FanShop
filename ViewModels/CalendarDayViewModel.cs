using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using FanShop.Models;
using FanShop.Services;
using FanShop.Windows;
using Microsoft.EntityFrameworkCore;

namespace FanShop.ViewModels
{
    public class CalendarDayViewModel : INotifyPropertyChanged
    {
        public DateTime Date { get; set; }
        private ObservableCollection<Employee> _employees;
        public ObservableCollection<Employee> Employees
        {
            get
            {
                if (_employees == null)
                {
                    using var context = new AppDbContext();
                    var workDay = context.WorkDays
                        .Include(w => w.Employees)
                        .FirstOrDefault(w => w.Date == Date);
        
                    _employees = workDay != null
                        ? new ObservableCollection<Employee>(workDay.Employees)
                        : new ObservableCollection<Employee>();
                }
                return _employees;
            }
            set
            {
                _employees = value;
                OnPropertyChanged(nameof(Employees));
            }
        }

        private MatchInfo _match;
        public MatchInfo Match
        {
            get => _match;
            set
            {
                if (_match != value)
                {
                    _match = value;
                    HasMatch = _match != null;
                    OnPropertyChanged(nameof(Match));
                }
            }
        }

        private bool _hasMatch;
        public bool HasMatch
        {
            get => _hasMatch;
            set
            {
                if (_hasMatch != value)
                {
                    _hasMatch = value;
                    OnPropertyChanged(nameof(HasMatch));
                }
            }
        }

        private bool _isCurrentMonth;
        public bool IsCurrentMonth
        {
            get => _isCurrentMonth;
            set
            {
                if (_isCurrentMonth != value)
                {
                    _isCurrentMonth = value;
                    OnPropertyChanged(nameof(IsCurrentMonth));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        public ICommand ShowDayDetailsCommand { get; }
        public ICommand AddEmployeesCommand { get; }
        public ICommand RemoveEmployeesCommand { get; }
        public ICommand PrintPassCommand { get; }
        
        private Employee? _selectedEmployee;

        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                _selectedEmployee = value;
                OnPropertyChanged(nameof(SelectedEmployee));
                (RemoveEmployeesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
        
        public CalendarDayViewModel()
        {
            ShowDayDetailsCommand = new RelayCommand(ShowDayDetails);
            AddEmployeesCommand = new RelayCommand(AddEmployees);
            RemoveEmployeesCommand = new RelayCommand(RemoveEmployees, CanEditEmployee);
            PrintPassCommand = new RelayCommand(PrintPass);
        }

        private void ShowDayDetails(object? parameter)
        {
            if (!HasMatch)
            {
                var dayDetailsWindow = new DayDetailsWindow
                {
                    DataContext = this
                };
                dayDetailsWindow.ShowDialog();
            }
        }
        
        private void AddEmployees(object? parameter)
        {
            using var context = new AppDbContext();
            var employeeWindowViewModel = new EmployeeWindowViewModel();
        
            var availableEmployees = employeeWindowViewModel.Employees
                .Where(e => !Employees.Any(existing => existing.EmployeeID == e.EmployeeID))
                .ToList();
        
            employeeWindowViewModel.Employees = new ObservableCollection<Employee>(availableEmployees);
        
            var selectEmployeeWindow = new SelectEmployeeWindow
            {
                DataContext = employeeWindowViewModel
            };
        
            if (selectEmployeeWindow.ShowDialog() == true && selectEmployeeWindow.SelectedEmployee != null)
            {
                var selectedEmployee = selectEmployeeWindow.SelectedEmployee;
        
                Employees.Add(selectedEmployee);
        
                var trackedEmployee = context.Employees.Local.FirstOrDefault(e => e.EmployeeID == selectedEmployee.EmployeeID);
                if (trackedEmployee == null)
                {
                    trackedEmployee = context.Employees.FirstOrDefault(e => e.EmployeeID == selectedEmployee.EmployeeID);
                    if (trackedEmployee == null)
                    {
                        context.Employees.Add(selectedEmployee);
                        trackedEmployee = selectedEmployee;
                    }
                }
        
                var workDay = context.WorkDays.FirstOrDefault(w => w.Date == Date) ?? new WorkDay { Date = Date };
                if (!workDay.Employees.Any(e => e.EmployeeID == trackedEmployee.EmployeeID))
                {
                    workDay.Employees.Add(trackedEmployee);
                }
        
                if (workDay.WorkDayID == 0)
                {
                    context.WorkDays.Add(workDay);
                }
        
                context.SaveChanges();
        
                OnPropertyChanged(nameof(Employees));
                OnPropertyChanged(nameof(DisplayedEmployees));
            }
        }
        
        private void RemoveEmployees(object? parameter)
        {
            if (SelectedEmployee != null)
            {
                using var context = new AppDbContext();

                var workDay = context.WorkDays
                    .Include(w => w.Employees)
                    .FirstOrDefault(w => w.Date == Date);

                if (workDay != null)
                {
                    var employeeToRemove = workDay.Employees
                        .FirstOrDefault(e => e.EmployeeID == SelectedEmployee.EmployeeID);

                    if (employeeToRemove != null)
                    {
                        workDay.Employees.Remove(employeeToRemove);
                        context.SaveChanges();
                    }
                }

                Employees.Remove(SelectedEmployee);
                SelectedEmployee = null;

                OnPropertyChanged(nameof(Employees));
                OnPropertyChanged(nameof(DisplayedEmployees));
            }
        }
        
        private bool CanEditEmployee(object? parameter)
        {
            return SelectedEmployee != null;
        }

        private void PrintPass(object? parameter)
        {
            MessageBox.Show("Пропуск распечатан!");
        }

        private string _additionalEmployeesText;
        public string AdditionalEmployeesText
        {
            get => _additionalEmployeesText;
            set
            {
                if (_additionalEmployeesText != value)
                {
                    _additionalEmployeesText = value;
                    OnPropertyChanged(nameof(AdditionalEmployeesText));
                }
            }
        }

        private bool _isAdditionalEmployeesTextVisible;
        public bool IsAdditionalEmployeesTextVisible
        {
            get => _isAdditionalEmployeesTextVisible;
            set
            {
                if (_isAdditionalEmployeesTextVisible != value)
                {
                    _isAdditionalEmployeesTextVisible = value;
                    OnPropertyChanged(nameof(IsAdditionalEmployeesTextVisible));
                }
            }
        }
        
        public IEnumerable<object> DisplayedEmployees
        {
            get
            {
                var displayed = Employees.Take(3).Cast<object>().ToList();
                if (Employees.Count > 3)
                {
                    AdditionalEmployeesText = ($"Ещё {Employees.Count - 3}");
                    IsAdditionalEmployeesTextVisible = true;
                }
                return displayed.Select(e => e is Employee emp ? emp.FirstName : e);;
            }
        }

    }

    public class MatchInfo
    {
        public string TeamName { get; set; }
        public string Time { get; set; }
        public string SartTime { get; set; }
        public BitmapImage Logo { get; set; }
    }
}