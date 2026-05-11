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
        Close();
    }

    private void SelectEmployee()
    {
        if (DataContext is not EmployeeViewModel viewModel || viewModel.SelectedEmployee is not Employee employee || ParentViewModel == null)
            return;

        var selectedItem = WorkDurationComboBox.SelectedItem as ComboBoxItem;
        var workDuration = selectedItem?.Content?.ToString() ?? "Целый день";

        using var context = new AppDbContext();
        var workDay = context.WorkDays
            .Include(x => x.WorkDayEmployees)
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

        var existingAssignment = workDay.WorkDayEmployees.FirstOrDefault(x => x.EmployeeID == employee.EmployeeID);
        if (existingAssignment == null)
        {
            existingAssignment = new WorkDayEmployee
            {
                WorkDayID = workDay.WorkDayID,
                EmployeeID = employee.EmployeeID,
                WorkDuration = workDuration
            };
            context.WorkDayEmployees.Add(existingAssignment);
            context.SaveChanges();
        }

        if (!ParentViewModel.Employees.Any(x => x.Employee.EmployeeID == employee.EmployeeID))
        {
            ParentViewModel.AddEmployeeToDay(employee, workDuration, existingAssignment.WorkDayEmployeeID);
        }

        Close();
    }
}
