using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FanShop.Models;
using FanShop.Services;
using Application = System.Windows.Application;

namespace FanShop.ViewModels;

public class TaskCategoriesWindowViewModel : BaseViewModel
{
    private ObservableCollection<TaskCategory> _taskCategories = new();

    public ObservableCollection<TaskCategory> TaskCategories
    {
        get => _taskCategories;
        set => SetProperty(ref _taskCategories, value);
    }

    public ICommand AddCategoryCommand { get; }
    public ICommand EditCategoryCommand { get; }
    public ICommand RemoveCategoryCommand { get; }
    public ICommand CloseWindowCommand { get; }
    public ICommand SaveEditedCategoryCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand GenerateRandomColorCommand { get; }
    public ICommand OpenColorPickerCommand { get; }

    private bool _isEditOverlayVisible;

    public bool IsEditOverlayVisible
    {
        get => _isEditOverlayVisible;
        set => SetProperty(ref _isEditOverlayVisible, value);
    }

    private TaskCategory? _editableCategory;

    public TaskCategory? EditableCategory
    {
        get => _editableCategory;
        set
        {
            if (SetProperty(ref _editableCategory, value))
            {
                OnPropertyChanged(nameof(CanSaveCategory));
            }
        }
    }

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

    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _color = string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            if (EditableCategory != null)
                EditableCategory.Name = value;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(CanSaveCategory));
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            _description = value;
            if (EditableCategory != null)
                EditableCategory.Description = value;
            OnPropertyChanged(nameof(Description));
        }
    }

    public string Color
    {
        get => _color;
        set
        {
            _color = value;
            if (EditableCategory != null)
                EditableCategory.Color = value;
            OnPropertyChanged(nameof(Color));
        }
    }
    
    private ColorGenerator ColorGenerator { get; set; }

    public bool CanSaveCategory
    {
        get
        {
            return !string.IsNullOrWhiteSpace(_name) &&
                   !string.IsNullOrWhiteSpace(_color) &&
                   ColorGenerator.IsValidHexColor(_color);
        }
    }

    public TaskCategoriesWindowViewModel()
    {
        ColorGenerator = new ColorGenerator();
        LoadCategories();

        AddCategoryCommand = new RelayCommand(AddCategory);
        EditCategoryCommand = new RelayCommand(EditCategory, CanEditCategory);
        RemoveCategoryCommand = new RelayCommand(RemoveCategory, CanEditCategory);
        CloseWindowCommand = new RelayCommand(CloseWindow);
        SaveEditedCategoryCommand = new RelayCommand(SaveEditedCategory);
        CancelEditCommand = new RelayCommand(CancelEdit);
        GenerateRandomColorCommand = new RelayCommand(GenerateRandomColor);
        OpenColorPickerCommand = new RelayCommand(OpenColorPicker);
    }

    private void LoadCategories()
    {
        using var context = new AppDbContext();
        var categories = context.TaskCategories.ToList();
        TaskCategories = new ObservableCollection<TaskCategory>(categories);
    }

    private void AddCategory(object? parameter)
    {
        SelectedCategory = null;
        EditableCategory = new TaskCategory();

        Name = string.Empty;
        Description = string.Empty;
        var existingColors = TaskCategories.Select(c => c.Color).ToHashSet();
        Color = ColorGenerator.GenerateUniquePastelColor(existingColors);

        IsEditOverlayVisible = true;
    }

    private void EditCategory(object? parameter)
    {
        if (SelectedCategory != null)
        {
            EditableCategory = new TaskCategory();

            Name = SelectedCategory.Name;
            Description = SelectedCategory.Description ?? string.Empty;
            Color = SelectedCategory.Color;

            IsEditOverlayVisible = true;
        }
    }

    private void SaveEditedCategory(object? parameter)
    {
        using var context = new AppDbContext();
        if (EditableCategory != null && CanSaveCategory)
        {
            if (SelectedCategory == null)
            {
                context.TaskCategories.Add(EditableCategory);
            }
            else
            {
                var category = context.TaskCategories.Find(SelectedCategory.TaskCategoryID);
                if (category != null)
                {
                    category.Name = EditableCategory.Name;
                    category.Description = EditableCategory.Description;
                    category.Color = EditableCategory.Color;
                    context.TaskCategories.Update(category);
                }
            }

            context.SaveChanges();
            IsEditOverlayVisible = false;
            LoadCategories();
        }
    }
    
    private void OpenColorPicker(object? parameter)
    {
        var colorDialog = new System.Windows.Forms.ColorDialog
        {
            AllowFullOpen = true,
            AnyColor = true,
            SolidColorOnly = false,
            CustomColors = new int[] { }
        };

        try
        {
            if (!string.IsNullOrEmpty(Color) && Color.StartsWith("#"))
            {
                var color = ColorTranslator.FromHtml(Color);
                colorDialog.Color = color;
            }
        }
        catch
        {
        }

        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            Color = $"#{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}";
        }
    }

    private void CancelEdit(object? parameter)
    {
        IsEditOverlayVisible = false;
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

    private void GenerateRandomColor(object? parameter)
    {
        var existingColors = TaskCategories.Select(c => c.Color).ToHashSet();
        Color = ColorGenerator.GenerateUniquePastelColor(existingColors);
    }

    private void CloseWindow(object? parameter)
    {
        Application.Current.Windows.OfType<Window>()
            .FirstOrDefault(w => w.GetType().Name == "TaskCategoriesWindow")?.Close();
    }
}