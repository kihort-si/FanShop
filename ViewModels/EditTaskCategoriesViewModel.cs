using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;
using FanShop.Services;
using FanShop.Windows;

namespace FanShop.ViewModels;

public partial class EditTaskCategoriesViewModel : BaseViewModel
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly TaskCategoriesViewModel _taskCategoriesViewModel;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _color = string.Empty;

    [ObservableProperty]
    private string _defaultTask = string.Empty;

    [ObservableProperty]
    private TaskCategory? _editableCategory;

    [ObservableProperty]
    private TaskCategory? _selectedCategory;
    
    [ObservableProperty]
    private bool _isPaletteOpen;

    public IReadOnlyList<string> PaletteColors { get; } =
    [
        "#F8BBD0",
        "#F48FB1",
        "#CE93D8",
        "#B39DDB",
        "#9FA8DA",

        "#90CAF9",
        "#81D4FA",
        "#80DEEA",
        "#80CBC4",
        "#A5D6A7",

        "#C5E1A5",
        "#E6EE9C",
        "#FFF59D",
        "#FFE082",
        "#FFCC80",

        "#FFAB91",
        "#BCAAA4",
        "#CFD8DC",
        "#AED581",
        "#4DB6AC"
    ];
    
    private ColorGenerator ColorGenerator { get; set; }

    public bool CanSaveCategory =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(Color) &&
        ColorGenerator.IsValidHexColor(Color);

    public EditTaskCategoriesViewModel(TaskCategory selectedCategory, MainWindowViewModel mainWindowViewModel,
        TaskCategoriesViewModel taskCategoriesViewModel)
    {
        ColorGenerator = new ColorGenerator();
        SelectedCategory = selectedCategory;
        EditCategory();
        _mainWindowViewModel = mainWindowViewModel;
        _taskCategoriesViewModel = taskCategoriesViewModel;
    }

    public EditTaskCategoriesViewModel(MainWindowViewModel mainWindowViewModel,
        TaskCategoriesViewModel taskCategoriesViewModel)
    {
        ColorGenerator = new ColorGenerator();
        _mainWindowViewModel = mainWindowViewModel;
        _taskCategoriesViewModel = taskCategoriesViewModel;
        AddCategory();
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

    [RelayCommand]
    private void SaveEditedCategory()
    {
        if (!CanSaveCategory) return;

        using var context = new AppDbContext();
        if (EditableCategory != null)
        {
            EditableCategory.Name = Name;
            EditableCategory.Description = Description;
            EditableCategory.Color = Color;
            EditableCategory.DefaultTask = DefaultTask;

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

    [RelayCommand]
    private void CancelEdit()
    {
        _mainWindowViewModel.CloseTabRequest(this);
    }

    [RelayCommand]
    private void OpenColorPicker()
    {
        IsPaletteOpen = !IsPaletteOpen;
    }
    
    [RelayCommand]
    private void SelectColor(string color)
    {
        Color = color;
        IsPaletteOpen = false;
    }

    [RelayCommand]
    private void GenerateRandomColor()
    {
        var existingColors = _taskCategoriesViewModel.TaskCategories.Select(c => c.Color).ToHashSet();
        Color = ColorGenerator.GenerateUniquePastelColor(existingColors);
    }
}
