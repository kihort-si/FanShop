using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FanShop.Models;
using FanShop.Services;
using Microsoft.EntityFrameworkCore;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace FanShop.ViewModels
{
    public class DayTasksWindowViewModel : BaseViewModel
    {
        private DateTime _date;
        public DateTime Date
        {
            get => _date;
            set => SetProperty(ref _date, value);
        }

        private ObservableCollection<DayTask> _dayTasks;
        public ObservableCollection<DayTask> DayTasks
        {
            get => _dayTasks;
            set => SetProperty(ref _dayTasks, value);
        }

        private ObservableCollection<TaskCategory> _taskCategories;
        public ObservableCollection<TaskCategory> TaskCategories
        {
            get => _taskCategories;
            set => SetProperty(ref _taskCategories, value);
        }

        private DayTask _selectedTask;
        public DayTask SelectedTask
        {
            get => _selectedTask;
            set
            {
                if (SetProperty(ref _selectedTask, value))
                {
                    (RemoveTaskCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _taskTitle;
        public string TaskTitle
        {
            get => _taskTitle;
            set
            {
                if (SetProperty(ref _taskTitle, value))
                {
                    UpdateCanSaveTask();
                }
            }
        }

        private string _startTimeText;
        public string StartTimeText
        {
            get => _startTimeText;
            set
            {
                if (SetProperty(ref _startTimeText, value))
                {
                    UpdateCanSaveTask();
                }
            }
        }

        private string _endTimeText;
        public string EndTimeText
        {
            get => _endTimeText;
            set
            {
                if (SetProperty(ref _endTimeText, value))
                {
                    UpdateCanSaveTask();
                }
            }
        }

        private bool _canSaveTask;
        public bool CanSaveTask
        {
            get => _canSaveTask;
            set => SetProperty(ref _canSaveTask, value);
        }

        public ICommand AddTaskCommand { get; }
        public ICommand RemoveTaskCommand { get; } 
        public ICommand CloseWindowCommand { get; }

        public DayTasksWindowViewModel(DateTime date)
        {
            Date = date;
            
            AddTaskCommand = new RelayCommand(AddTask);
            RemoveTaskCommand = new RelayCommand(RemoveTask, CanEditTask);
            CloseWindowCommand = new RelayCommand(CloseWindow);

            LoadData();
        }

        private void LoadData()
        {
            using var context = new AppDbContext();
        
            var tasks = context.DayTasks
                .Include(t => t.Category)
                .Where(t => t.Date.Date == Date.Date)
                .ToList() 
                .OrderBy(t => t.StartTime) 
                .ToList();
        
            DayTasks = new ObservableCollection<DayTask>(tasks);
        
            var categories = context.TaskCategories.ToList();
            TaskCategories = new ObservableCollection<TaskCategory>(categories);
        }

        private void AddTask(object parameter)
        {
            var newTask = new DayTask
            {
                Date = Date,
                Title = "Новая задача",
                Comment = string.Empty,
                StartTimeText = DateTime.Now.ToShortTimeString(),
                EndTimeText = DateTime.Now.AddHours(1).ToShortTimeString(),
                StartHour = DateTime.Now.Hour,
                StartMinute = DateTime.Now.Minute,
                EndHour = DateTime.Now.AddHours(1).Hour,
                EndMinute = DateTime.Now.AddHours(1).Minute
            };
    
            DayTasks.Add(newTask);
        }

        private bool CanEditTask(object parameter)
        {
            return SelectedTask != null;
        }

        private void RemoveTask(object parameter)
        {
            if (SelectedTask != null)
            {
                var result = MessageBox.Show(
                    $"Удалить задачу \"{SelectedTask.Title}\"?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    using var context = new AppDbContext();
                    var taskToRemove = context.DayTasks.Find(SelectedTask.DayTaskID);
                    if (taskToRemove != null)
                    {
                        context.DayTasks.Remove(taskToRemove);
                        context.SaveChanges();
                    }

                    DayTasks.Remove(SelectedTask);
                    SelectedTask = null;
                }
            }
        }
        
        public void SaveTaskChanges(object taskItem)
        {
            if (taskItem is not DayTask task) return;
        
            try
            {
                using var context = new AppDbContext();
                
                if (task.DayTaskID == 0)
                {
                    // Новая задача
                    context.DayTasks.Add(task);
                }
                else
                {
                    // Существующая задача - используем более надежный подход
                    var existingTask = context.DayTasks.Find(task.DayTaskID);
                    if (existingTask != null)
                    {
                        // Обновляем свойства напрямую
                        existingTask.StartHour = task.StartHour;
                        existingTask.StartMinute = task.StartMinute;
                        existingTask.EndHour = task.EndHour;
                        existingTask.EndMinute = task.EndMinute;
                        existingTask.Title = task.Title ?? string.Empty;
                        existingTask.Comment = task.Comment ?? string.Empty;
                        existingTask.TaskCategoryID = task.TaskCategoryID;
                    }
                    else
                    {
                        // Отсоединенное состояние - явно указываем что это модификация
                        context.Entry(task).State = EntityState.Modified;
                    }
                }
        
                context.SaveChanges();
            }
            catch (DbUpdateException dbEx)
            {
                // Детальная информация об ошибке базы данных
                var innerException = dbEx.InnerException;
                MessageBox.Show($"Ошибка базы данных: {innerException?.Message ?? dbEx.Message}", 
                    "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
                
                Console.WriteLine($"DbUpdateException: {dbEx}");
                Console.WriteLine($"Inner Exception: {dbEx.InnerException}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseWindow(object parameter)
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
            }
        }

        private void UpdateCanSaveTask()
        {
            CanSaveTask = !string.IsNullOrWhiteSpace(TaskTitle) &&
                         !string.IsNullOrWhiteSpace(StartTimeText) &&
                         !string.IsNullOrWhiteSpace(EndTimeText);
        }
    }
}