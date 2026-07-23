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
    public Employee? SelectedEmployee { get; private set; }
    public string SelectedWorkDuration { get; private set; } = "Целый день";
    public Position? SelectedPosition { get; private set; }

    public SelectEmployeeWindow()
    {
        InitializeComponent();
    }

    private void EmployeesGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        SelectEmployee();
    }

    private void SelectButton_Click(object? sender, RoutedEventArgs e)
    {
        SelectEmployee();
    }

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
            DialogService.ShowInfo(
                "Не выбрана должность.");
            return;
        }

        if (SelectionOnly)
        {
            SelectedEmployee = employee;
            SelectedWorkDuration = workDuration;
            SelectedPosition = selectedPosition;
            Close(true);
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
            existingAssignment = new WorkDayEmployee
            {
                WorkDayID = workDay.WorkDayID,
                EmployeeID = employee.EmployeeID,
                WorkDuration = workDuration,
                PositionID = selectedPosition.PositionID,
                SalaryAtMoment = salaryService.GetSalaryForShift(
                    selectedPosition.PositionID,
                    ParentViewModel.Date,
                    workDuration)
            };

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
            selectedPosition.PositionName);
        }

        Close(true);
    }
}
