using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FanShop.Models;
using FanShop.Services;
using FanShop.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace FanShop.Windows;

public partial class SelectEmployeeWindow : Window
{
    public CalendarDayViewModel? ParentViewModel { get; set; }
    public bool SelectionOnly { get; set; }
    public List<EmployeeShiftSelection> Selections { get; } = new();
    public Employee? SelectedEmployee { get; private set; }
    public string SelectedWorkDuration { get; private set; } = "Целый день";
    public Position? SelectedPosition { get; private set; }

    public SelectEmployeeWindow()
    {
        InitializeComponent();
        Opened += (_, _) => CancelButton.IsVisible = SelectionOnly;
    }

    private void EmployeesGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        SelectEmployee();
    }

    private void SelectButton_Click(object? sender, RoutedEventArgs e)
    {
        SelectEmployee();
    }

    private void DoneButton_Click(object? sender, RoutedEventArgs e) => Close(!SelectionOnly || Selections.Count > 0);

    private void MarkAdded(EmployeeViewModel viewModel, Employee employee)
    {
        viewModel.ExcludeEmployees(new[] { employee.EmployeeID });
        AddedCountText.Text = $"Добавлено: {++_addedCount}";
    }
    private int _addedCount;

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void SelectEmployee()
    {
        if (DataContext is not EmployeeViewModel viewModel || viewModel.SelectedEmployee is not Employee employee)
            return;

        var selectedItem = WorkDurationComboBox.SelectedItem as ComboBoxItem;
        var workDuration = selectedItem?.Content?.ToString() ?? "Целый день";
        var selectedPosition = viewModel.SelectedPosition;

        if (selectedPosition == null)
        {
            _ = DialogService.ShowInfo(
                "Не выбрана должность.");
            return;
        }

        if (SelectionOnly)
        {
            SelectedEmployee = employee;
            SelectedWorkDuration = workDuration;
            SelectedPosition = selectedPosition;
            Selections.Add(new EmployeeShiftSelection(employee, workDuration, selectedPosition));
            MarkAdded(viewModel, employee);
            return;
        }

        if (ParentViewModel == null)
            return;

        using var context = new AppDbContext();
        var workDay = context.WorkDays
            .Include(x => x.WorkDayEmployee)
            .FirstOrDefault(x => x.Date.Date == ParentViewModel.Date.Date);

        if (workDay == null)
        {
            workDay = new WorkDay
            {
                Date = ParentViewModel.Date.Date
            };
            context.WorkDays.Add(workDay);
            context.SaveChanges();
        }

        var existingAssignment = context.WorkDayEmployee
            .FirstOrDefault(x =>
                x.WorkDayID == workDay.WorkDayID &&
                x.EmployeeID == employee.EmployeeID);
        var salaryService = new SalaryService(context);

        if (existingAssignment == null)
        {
            existingAssignment = EmployeeShiftDefaults.Create(employee, workDay.WorkDayID,
                ParentViewModel.Date, selectedPosition, workDuration, salaryService);

            context.WorkDayEmployee.Add(existingAssignment);
            context.SaveChanges();
        } else
        {
            existingAssignment.WorkDuration = workDuration;
            existingAssignment.PositionID = selectedPosition.PositionID;

            existingAssignment.SalaryAtMoment =
                salaryService.GetSalaryForShift(
                    selectedPosition.PositionID,
                    ParentViewModel.Date,
                    workDuration);

            context.SaveChanges();
        }

        if (!ParentViewModel.Employees.Any(x => x.Employee.EmployeeID == employee.EmployeeID))
        {
            ParentViewModel.AddEmployeeToDay(
            employee,
            workDuration,
            existingAssignment.WorkDayEmployeeID,
            selectedPosition.PositionID,
            selectedPosition.PositionName);
        }

        MarkAdded(viewModel, employee);
    }
}
