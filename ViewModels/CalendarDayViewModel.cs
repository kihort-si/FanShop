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
    public class CalendarDayViewModel : BaseViewModel
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
                        .Include(w => w.WorkDayEmployees)
                        .ThenInclude(wde => wde.Employee)
                        .FirstOrDefault(w => w.Date == Date);

                    _employees = workDay != null
                        ? new ObservableCollection<Employee>(workDay.WorkDayEmployees.Select(wde => wde.Employee))
                        : new ObservableCollection<Employee>();
                }

                return _employees;
            }
            set => SetProperty(ref _employees, value);
        }

        private MatchInfo _match;

        public MatchInfo Match
        {
            get => _match;
            set
            {
                if (SetProperty(ref _match, value))
                {
                    HasMatch = _match != null;
                }
            }
        }

        private bool _hasMatch;

        public bool HasMatch
        {
            get => _hasMatch;
            set => SetProperty(ref _hasMatch, value);
        }

        private bool _isCurrentMonth;

        public bool IsCurrentMonth
        {
            get => _isCurrentMonth;
            set => SetProperty(ref _isCurrentMonth, value);
        }

        public ICommand ShowDayDetailsCommand { get; }
        public ICommand AddEmployeesCommand { get; }
        public ICommand RemoveEmployeesCommand { get; }
        public ICommand PrintPassCommand { get; }
        public ICommand CloseWindowCommand { get; }

        private Employee? _selectedEmployee;

        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (SetProperty(ref _selectedEmployee, value))
                {
                    (RemoveEmployeesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public CalendarDayViewModel()
        {
            ShowDayDetailsCommand = new RelayCommand(ShowDayDetails);
            AddEmployeesCommand = new RelayCommand(AddEmployees);
            RemoveEmployeesCommand = new RelayCommand(RemoveEmployees, CanEditEmployee);
            PrintPassCommand = new RelayCommand(PrintPass);
            CloseWindowCommand = new RelayCommand(CloseWindow);
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
                var selectedWorkDuration = selectEmployeeWindow.SelectedWorkDuration;

                var workDay = context.WorkDays
                    .Include(w => w.WorkDayEmployees)
                    .FirstOrDefault(w => w.Date == Date) ?? new WorkDay { Date = Date };

                var workDayEmployee = new WorkDayEmployee
                {
                    EmployeeID = selectedEmployee.EmployeeID,
                    WorkDayID = workDay.WorkDayID,
                    WorkDuration = selectedWorkDuration
                };

                workDay.WorkDayEmployees.Add(workDayEmployee);

                if (workDay.WorkDayID == 0)
                {
                    context.WorkDays.Add(workDay);
                }

                context.SaveChanges();

                Employees.Add(selectedEmployee);

                NotifyMainWindowOfChanges();

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
                    .Include(w => w.WorkDayEmployees)
                    .FirstOrDefault(w => w.Date == Date);

                if (workDay != null)
                {
                    var workDayEmployeeToRemove = workDay.WorkDayEmployees
                        .FirstOrDefault(wde => wde.EmployeeID == SelectedEmployee.EmployeeID);

                    if (workDayEmployeeToRemove != null)
                    {
                        context.Remove(workDayEmployeeToRemove);
                        context.SaveChanges();
                    }
                }

                Employees.Remove(SelectedEmployee);
                SelectedEmployee = null;

                NotifyMainWindowOfChanges();

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
            if (Employees.Count == 0)
            {
                MessageBox.Show("Нет сотрудников для создания пропуска.");
                return;
            }

            PassDocumentGenerator.CreateAndShowPassPdf(Date, Employees);
        }

        private void CloseWindow(object? parameter)
        {
            Application.Current.Windows[1]?.Close();
        }

        private string _additionalEmployeesText;

        public string AdditionalEmployeesText
        {
            get => _additionalEmployeesText;
            set => SetProperty(ref _additionalEmployeesText, value);
        }

        private bool _isAdditionalEmployeesTextVisible;

        public bool IsAdditionalEmployeesTextVisible
        {
            get => _isAdditionalEmployeesTextVisible;
            set => SetProperty(ref _isAdditionalEmployeesTextVisible, value);
        }

        public IEnumerable<object> DisplayedEmployees
        {
            get
            {
                var displayed = Employees.Take(4).Select(e => e.FirstName).ToList();

                if (Employees.Count > 4)
                {
                    AdditionalEmployeesText = $"Ещё {Employees.Count - 4}";
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

        private void NotifyMainWindowOfChanges()
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow?.DataContext is MainWindowViewModel mainViewModel)
            {
                mainViewModel.RefreshStatistics();
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