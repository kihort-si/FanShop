using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FanShop.Models;
using FanShop.Services;
using FanShop.Utils;
using FanShop.View;
using FanShop.Windows;
using Application = System.Windows.Application;

namespace FanShop.ViewModels;

public class TaskCategoriesViewModel : BaseViewModel
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    
    private ObservableCollection<TaskCategory> _taskCategories = new();

    public ObservableCollection<TaskCategory> TaskCategories
    {
        get => _taskCategories;
        set => SetProperty(ref _taskCategories, value);
    }

    public ICommand AddCategoryCommand { get; }
    public ICommand EditCategoryCommand { get; }
    public ICommand RemoveCategoryCommand { get; }
    public ICommand OpenAnalyticsCommand { get; }
    public ICommand CloseWindowCommand { get; }

    private TaskCategory? _selectedCategory;

    public TaskCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged(nameof(SelectedCategory));
            (RemoveCategoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (EditCategoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public TaskCategoriesViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
        
        LoadCategories();

        AddCategoryCommand = new RelayCommand(AddCategory);
        EditCategoryCommand = new RelayCommand(EditCategory, CanEditCategory);
        RemoveCategoryCommand = new RelayCommand(RemoveCategory, CanEditCategory);
        CloseWindowCommand = new RelayCommand(CloseWindow);
        OpenAnalyticsCommand = new RelayCommand(OpenAnalytics);
    }

    public void LoadCategories()
    {
        using var context = new AppDbContext();
        var categories = context.TaskCategories.ToList();
        TaskCategories = new ObservableCollection<TaskCategory>(categories);
    }
    
    private void AddCategory(object? parameter)
    {
        var editTaskCategoriesViewModel = new EditTaskCategoriesViewModel(_mainWindowViewModel, this);
        var editTaskCategoriesControl = new EditTaskCategoriesControl();
        _mainWindowViewModel.OpenTabRequest(editTaskCategoriesViewModel, editTaskCategoriesControl, "Новая категория");
    }

    private void EditCategory(object? parameter)
    {
        if (SelectedCategory != null)
        {
            var editTaskCategoriesViewModel = new EditTaskCategoriesViewModel(SelectedCategory, _mainWindowViewModel, this);
            var editTaskCategoriesControl = new EditTaskCategoriesControl();
            _mainWindowViewModel.OpenTabRequest(editTaskCategoriesViewModel, editTaskCategoriesControl,
                $"{SelectedCategory.Name}");
        }
    }

    private void RemoveCategory(object? parameter)
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

    private bool CanEditCategory(object? parameter)
    {
        return SelectedCategory != null;
    }
    
    private void OpenAnalytics(object? parameter)
    {
        var taskAnalyticsViewModel = new TaskAnalyticsViewModel(_mainWindowViewModel);
        var taskAnalyticsControl = new TaskAnalyticsControl();
        _mainWindowViewModel.OpenTabRequest(taskAnalyticsViewModel, taskAnalyticsControl, "Аналитика задач");
    }

    private void CloseWindow(object? parameter)
    {
        _mainWindowViewModel.CloseTabRequest(this);
    }
}