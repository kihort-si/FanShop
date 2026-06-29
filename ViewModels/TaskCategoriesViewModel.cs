using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;
using FanShop.Services;
using FanShop.View;
using FanShop.Windows;

namespace FanShop.ViewModels;

public partial class TaskCategoriesViewModel : BaseViewModel
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    [ObservableProperty]
    private ObservableCollection<TaskCategory> _taskCategories = new();

    [ObservableProperty]
    private TaskCategory? _selectedCategory;

    public TaskCategoriesViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
        LoadCategories();
    }

    partial void OnSelectedCategoryChanged(TaskCategory? value)
    {
        RemoveCategoryCommand.NotifyCanExecuteChanged();
        EditCategoryCommand.NotifyCanExecuteChanged();
    }

    public void LoadCategories()
    {
        using var context = new AppDbContext();
        var categories = context.TaskCategories.ToList();
        TaskCategories = new ObservableCollection<TaskCategory>(categories);
    }

    [RelayCommand]
    private void AddCategory()
    {
        var editTaskCategoriesViewModel = new EditTaskCategoriesViewModel(_mainWindowViewModel, this);
        var editTaskCategoriesControl = new EditTaskCategoriesControl();
        _mainWindowViewModel.OpenTabRequest(editTaskCategoriesViewModel, editTaskCategoriesControl, "Новая категория");
    }

    [RelayCommand(CanExecute = nameof(CanEditCategory))]
    private void EditCategory()
    {
        if (SelectedCategory != null)
        {
            var editTaskCategoriesViewModel = new EditTaskCategoriesViewModel(SelectedCategory, _mainWindowViewModel, this);
            var editTaskCategoriesControl = new EditTaskCategoriesControl();
            _mainWindowViewModel.OpenTabRequest(editTaskCategoriesViewModel, editTaskCategoriesControl,
                $"{SelectedCategory.Name}");
        }
    }

    private bool CanEditCategory => SelectedCategory != null;

    [RelayCommand(CanExecute = nameof(CanRemoveCategory))]
    private void RemoveCategory()
    {
        using var context = new AppDbContext();
        if (SelectedCategory != null)
        {
            var category = context.TaskCategories.Find(SelectedCategory.TaskCategoryID);
            if (category != null)
            {
                context.TaskCategories.Remove(category);
                context.SaveChanges();
                TaskCategories.Remove(SelectedCategory);
            }
        }
    }

    private bool CanRemoveCategory => SelectedCategory != null;

    [RelayCommand]
    private void OpenAnalytics()
    {
        var taskAnalyticsViewModel = new TaskAnalyticsViewModel(_mainWindowViewModel);
        var taskAnalyticsControl = new TaskAnalyticsControl();
        _mainWindowViewModel.OpenTabRequest(taskAnalyticsViewModel, taskAnalyticsControl, "Аналитика задач");
    }

    [RelayCommand]
    private void CloseWindow()
    {
        _mainWindowViewModel.CloseTabRequest(this);
    }
}
