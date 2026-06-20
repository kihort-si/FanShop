using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;
using FanShop.Services;
using Microsoft.EntityFrameworkCore;

namespace FanShop.ViewModels;

public partial class DayTasksWindowViewModel : BaseViewModel
{
    [ObservableProperty]
    private DateTime _date;

    [ObservableProperty]
    private ObservableCollection<DayTask> _dayTasks = new();

    [ObservableProperty]
    private ObservableCollection<TaskCategory> _taskCategories = new();

    [ObservableProperty]
    private DayTask? _selectedTask;

    [ObservableProperty]
    private string _taskTitle = string.Empty;

    [ObservableProperty]
    private string _startTimeText = string.Empty;

    [ObservableProperty]
    private string _endTimeText = string.Empty;

    [ObservableProperty]
    private bool _canSaveTask;

    [ObservableProperty]
    private List<string> _taskSuggestions = new();

    public DayTasksWindowViewModel(DateTime date)
    {
        Date = date;
        LoadData();
        LoadTaskSuggestions();
    }

    partial void OnTaskTitleChanged(string value)
    {
        UpdateCanSaveTask();
    }

    partial void OnStartTimeTextChanged(string value)
    {
        UpdateCanSaveTask();
    }

    partial void OnEndTimeTextChanged(string value)
    {
        UpdateCanSaveTask();
    }

    partial void OnSelectedTaskChanged(DayTask? value)
    {
        RemoveTaskCommand.NotifyCanExecuteChanged();
        SaveSelectedTaskCommand.NotifyCanExecuteChanged();
    }

    private void UpdateCanSaveTask()
    {
        CanSaveTask = !string.IsNullOrWhiteSpace(TaskTitle) &&
                     !string.IsNullOrWhiteSpace(StartTimeText) &&
                     !string.IsNullOrWhiteSpace(EndTimeText);
    }

    [RelayCommand]
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

    [RelayCommand]
    private void AddTask()
    {
        var newTask = new DayTask
        {
            Date = Date,
            Title = "Новая задача",
            Comment = string.Empty,
            StartTimeText = DateTime.Now.ToShortTimeString(),
            EndTimeText = DateTime.Now.AddMinutes(15).ToShortTimeString(),
            StartHour = DateTime.Now.Hour,
            StartMinute = DateTime.Now.Minute,
            EndHour = DateTime.Now.AddMinutes(15).Hour,
            EndMinute = DateTime.Now.AddMinutes(15).Minute
        };

        DayTasks.Add(newTask);
        SaveTaskChanges(newTask);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveTask))]
    private void RemoveTask()
    {
        if (SelectedTask != null)
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

    private bool CanRemoveTask => SelectedTask != null;

    [RelayCommand]
    private void ExportToExcel()
    {
        DateTime startDate = Date.Date;
        DateTime endDate = Date.Date;
        TaskExportToExcel.ExportToExcel(startDate, endDate);
    }

    [RelayCommand]
    private void CloseWindow()
    {
    }

    [RelayCommand(CanExecute = nameof(CanSaveSelectedTask))]
    private void SaveSelectedTask()
    {
        if (SelectedTask == null)
            return;

        SaveTaskChanges(SelectedTask);
        UpdateTaskTitleByCategory(SelectedTask);
    }

    private bool CanSaveSelectedTask => SelectedTask != null;

    public void SaveTaskChanges(object taskItem)
    {
        if (taskItem is DayTask task)
        {
            using var context = new AppDbContext();
            var existingTask = context.DayTasks.Find(task.DayTaskID);

            if (existingTask == null)
            {
                if (task.Category != null)
                {
                    task.TaskCategoryID = task.Category.TaskCategoryID;
                    task.Category = null;
                }

                context.DayTasks.Add(task);
            }
            else
            {
                existingTask.StartHour = task.StartHour;
                existingTask.StartMinute = task.StartMinute;
                existingTask.EndHour = task.EndHour;
                existingTask.EndMinute = task.EndMinute;
                existingTask.Title = task.Title;
                existingTask.Comment = task.Comment;
                existingTask.TaskCategoryID = task.Category?.TaskCategoryID;

                context.DayTasks.Update(existingTask);
            }

            context.SaveChanges();
        }
    }

    public void UpdateTaskTitleByCategory(DayTask task)
    {
        if (task?.Category != null && !string.IsNullOrEmpty(task.Category.DefaultTask))
        {
            task.Title = task.Category.DefaultTask;
            task.OnPropertyChanged(nameof(task.Title));

            SaveTaskChanges(task);
        }
    }

    private void LoadTaskSuggestions()
    {
        using var context = new AppDbContext();

        TaskSuggestions = context.DayTasks
            .Select(t => t.Title)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }
}
