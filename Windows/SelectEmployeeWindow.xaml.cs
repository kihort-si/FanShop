using System.Windows;
using FanShop.Models;
using FanShop.ViewModels;

namespace FanShop.Windows;

public partial class SelectEmployeeWindow : Window
{
    public Employee? SelectedEmployee { get; private set; }
    
    public SelectEmployeeWindow()
    {
        InitializeComponent();
    }
    
    private void ListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is EmployeeWindowViewModel viewModel && viewModel.SelectedEmployee != null)
        {
            SelectedEmployee = viewModel.SelectedEmployee;
            DialogResult = true;
            Close();
        }
    }
}