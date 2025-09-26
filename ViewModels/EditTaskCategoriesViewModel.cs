using System.Windows.Input;
using FanShop.Models;
using FanShop.Services;

namespace FanShop.ViewModels;

public class EditTaskCategoriesViewModel : BaseViewModel
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly TaskCategoriesViewModel _taskCategoriesViewModel;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _color = string.Empty;
    private string _defaultTask = string.Empty;

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
            OnPropertyChanged(nameof(CanSaveCategory));
        }
    }
    
    public string DefaultTask
    {
        get => _defaultTask;
        set
        {
            _defaultTask = value;
            if (EditableCategory != null)
                EditableCategory.DefaultTask = value;
            OnPropertyChanged(nameof(DefaultTask));
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
    
    private TaskCategory? _selectedСategory;

    public TaskCategory? SelectedCategory
    {
        get => _selectedСategory;
        set
        {
            _selectedСategory = value;
            OnPropertyChanged(nameof(SelectedCategory));
        }
    }
    
    public ICommand SaveEditedCategoryCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand GenerateRandomColorCommand { get; }
    public ICommand OpenColorPickerCommand { get; }
    
    public EditTaskCategoriesViewModel(TaskCategory selectedCategory, MainWindowViewModel mainWindowViewModel, 
        TaskCategoriesViewModel taskCategoriesViewModel)
    {
        ColorGenerator = new ColorGenerator();
        SelectedCategory = selectedCategory;
        EditCategory();
        _mainWindowViewModel = mainWindowViewModel;
        _taskCategoriesViewModel = taskCategoriesViewModel;
        SaveEditedCategoryCommand = new RelayCommand(SaveEditedCategory);
        CancelEditCommand = new RelayCommand(CancelEdit);
        GenerateRandomColorCommand = new RelayCommand(GenerateRandomColor);
        OpenColorPickerCommand = new RelayCommand(OpenColorPicker);
    }
    
    public EditTaskCategoriesViewModel(MainWindowViewModel mainWindowViewModel,
        TaskCategoriesViewModel taskCategoriesViewModel)
    {
        ColorGenerator = new ColorGenerator();
        _mainWindowViewModel = mainWindowViewModel;
        _taskCategoriesViewModel = taskCategoriesViewModel;
        ColorGenerator = new ColorGenerator();
        AddCategory();
        SaveEditedCategoryCommand = new RelayCommand(SaveEditedCategory);
        CancelEditCommand = new RelayCommand(CancelEdit);
        GenerateRandomColorCommand = new RelayCommand(GenerateRandomColor);
        OpenColorPickerCommand = new RelayCommand(OpenColorPicker);
    }
    
    private void AddCategory()
    {
        SelectedCategory = null;
        EditableCategory = new TaskCategory();

        Name = string.Empty;
        Description = string.Empty;
        var existingColors = _taskCategoriesViewModel.TaskCategories.Select(c => c.Color).ToHashSet();
        Color = ColorGenerator.GenerateUniquePastelColor(existingColors);
        DefaultTask = string.Empty;
    }

    private void EditCategory()
    {
        if (SelectedCategory != null)
        {
            EditableCategory = new TaskCategory();

            Name = SelectedCategory.Name;
            Description = SelectedCategory.Description ?? string.Empty;
            Color = SelectedCategory.Color;
            DefaultTask = SelectedCategory.DefaultTask ?? string.Empty;
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
                    category.DefaultTask = EditableCategory.DefaultTask;
                    context.TaskCategories.Update(category);
                }
            }

            context.SaveChanges();
            _taskCategoriesViewModel.LoadCategories();
            _mainWindowViewModel.CloseTabRequest(this);
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
    
    private void GenerateRandomColor(object? parameter)
    {
        var existingColors = _taskCategoriesViewModel.TaskCategories.Select(c => c.Color).ToHashSet();
        Color = ColorGenerator.GenerateUniquePastelColor(existingColors);
    }

    private void CancelEdit(object? parameter)
    {
        _mainWindowViewModel.CloseTabRequest(this);
    }
}