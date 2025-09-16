using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using FanShop.Models;
using FanShop.Services;
using FanShop.Utils;
using FanShop.Windows;
using Microsoft.EntityFrameworkCore;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace FanShop.ViewModels
{
    public class CalendarDayViewModel : BaseViewModel
    {
        public DateTime Date { get; set; }
        private ObservableCollection<EmployeeWorkInfo> _employees;
        
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
        
        private ObservableCollection<DayTask> _tasks;
        
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
        
        private EmployeeWorkInfo _selectedEmployee;
        
        public EmployeeWorkInfo SelectedEmployee
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

        private MatchInfo _match;

        public MatchInfo Match
        {
            get => _match;
            set
            {
                if (SetProperty(ref _match, value))
                {
                    HasMatch = _match != null;
                    OnPropertyChanged(nameof(ShowChangeNotice));
                }
            }
        }

        private bool _hasMatch;

        public bool HasMatch
        {
            get => _hasMatch;
            set => SetProperty(ref _hasMatch, value);
        }
        
        public bool ShowChangeNotice => HasMatch && Match != null && Match.CanChange;

        private bool _isCurrentMonth;

        public bool IsCurrentMonth
        {
            get => _isCurrentMonth;
            set => SetProperty(ref _isCurrentMonth, value);
        }
        
        private bool _isBlackoutMode;
        
        public bool IsBlackoutMode
        {
            get => _isBlackoutMode;
            set => SetProperty(ref _isBlackoutMode, value);
        }

        private bool _isEmployeeView;
        
        public bool IsEmployeeView
        {
            get => _isEmployeeView;
            set => SetProperty(ref _isEmployeeView, value);
        }

        public ICommand ShowDayDetailsCommand { get; }
        public ICommand AddEmployeesCommand { get; }
        public ICommand RemoveEmployeesCommand { get; }
        public ICommand PrintPassCommand { get; }
        public ICommand DailyScheduleCommand { get; }
        public ICommand CloseWindowCommand { get; }
        
        public CalendarDayViewModel()
        {
            ShowDayDetailsCommand = new RelayCommand(ShowDayDetails);
            AddEmployeesCommand = new RelayCommand(AddEmployees);
            RemoveEmployeesCommand = new RelayCommand(RemoveEmployees, CanEditEmployee);
            PrintPassCommand = new RelayCommand(PrintPass, CanPrintPass);
            DailyScheduleCommand = new RelayCommand(DaylySchedule);
            CloseWindowCommand = new RelayCommand(CloseWindow);
            
            IsEmployeeView = true;
        }

        private void ShowDayDetails(object? parameter)
        {
            if (!HasMatch)
            {
                var dayDetailsWindow = new DayDetailsWindow
                {
                    DataContext = this
                };
                
                dayDetailsWindow.Owner = Application.Current.MainWindow;
                var mainViewModel = Application.Current.MainWindow?.DataContext as MainWindowViewModel;
                if (mainViewModel != null)
                {
                    mainViewModel.SetBlackoutMode(true);
                }
                dayDetailsWindow.ShowInTaskbar = false;
                dayDetailsWindow.Show();
                OpenWindowsController.Register(dayDetailsWindow);
                
                dayDetailsWindow.Closed += (s, e) =>
                {
                    if (mainViewModel != null)
                    {
                        OpenWindowsController.Unregister(dayDetailsWindow);
                    }
                };
            }
        }

        private void AddEmployees(object? parameter)
        {
            using var context = new AppDbContext();
            var employeeWindowViewModel = new EmployeeWindowViewModel();

            var availableEmployees = employeeWindowViewModel.EmployeesWithStats
                .Where(e => !Employees.Any(existing => existing.Employee.EmployeeID == e.EmployeeID))
                .ToList();

            employeeWindowViewModel.EmployeesWithStats = new ObservableCollection<Employee>(availableEmployees);

            var selectEmployeeWindow = new SelectEmployeeWindow
            {
                DataContext = employeeWindowViewModel,
                ParentViewModel = this
            };
            SetBlackoutMode(true);
            selectEmployeeWindow.Owner = Application.Current.MainWindow;
            selectEmployeeWindow.ShowInTaskbar = false;

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

                Employees.Add(new EmployeeWorkInfo 
                { 
                    Employee = selectedEmployee,
                    WorkDuration = selectedWorkDuration
                });

                NotifyMainWindowOfChanges();

                OnPropertyChanged(nameof(Employees));
                OnPropertyChanged(nameof(DisplayedEmployees));
                (PrintPassCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
                        .FirstOrDefault(wde => wde.EmployeeID == SelectedEmployee.Employee.EmployeeID);

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
        
        private bool CanPrintPass(object? parameter)
        {
            return Employees.Count > 0;
        }

        private void PrintPass(object? parameter)
        {
            if (Employees.Count == 0)
            {
                MessageBox.Show("Нет сотрудников для создания пропуска.");
                return;
            }

            PassDocumentGenerator.CreateWordPass(Date, Employees);
        }

        private void DaylySchedule(object? parameter)
        {
            var dayTasksWindow = new DayTasksWindow
            {
                DataContext = new DayTasksWindowViewModel(Date)
            };
            dayTasksWindow.Owner = Application.Current.MainWindow;
            dayTasksWindow.Closed += (s, e) =>
            {
                
                OnPropertyChanged(nameof(DisplayedTasks));
                OnPropertyChanged(nameof(AdditionalTasksText));
                OnPropertyChanged(nameof(IsAdditionalTasksTextVisible));
            };
            dayTasksWindow.Show();
        }

        private void CloseWindow(object? parameter)
        {
            Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.GetType().Name == "DayDetailsWindow")?.Close();
            var mainViewModel = Application.Current.MainWindow?.DataContext as MainWindowViewModel;
            if (mainViewModel != null)
                mainViewModel.SetBlackoutMode(false);
        }
        
        public void SetBlackoutMode(bool isBlackout)
        {
            IsBlackoutMode = isBlackout;
            OnPropertyChanged(nameof(IsBlackoutMode));
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
        
        public string AdditionalTasksText { get; set; }
        public bool IsAdditionalTasksTextVisible { get; set; }

        public IEnumerable<object> DisplayedTasks
        {
            get
            {
                var displayed = Tasks.Take(4).Select(t => t.Title).ToList();

                if (Tasks.Count > 4)
                {
                    AdditionalTasksText = $"Ещё {Tasks.Count - 4} {GetTasksTextForm(Tasks.Count - 4)}";
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
        public bool CanChange { get; set; }
    }
    
    public class EmployeeWorkInfo
    {
        public Employee Employee { get; set; }
        public string WorkDuration { get; set; }
        
        public string FirstName => Employee.FirstName;
        public string Surname => Employee.Surname;
        public string DateOfBirth => Employee.DateOfBirth;
    }
}
