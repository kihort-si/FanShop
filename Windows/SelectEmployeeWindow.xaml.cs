using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FanShop.Models;
using FanShop.ViewModels;
using Application = System.Windows.Application;
using ComboBox = System.Windows.Controls.ComboBox;

namespace FanShop.Windows
{
    public partial class SelectEmployeeWindow : Window
    {
        public Employee? SelectedEmployee { get; private set; }
        public string SelectedWorkDuration { get; set; } = "Целый день";
        public CalendarDayViewModel ParentViewModel { get; set; }
        
        public SelectEmployeeWindow()
        {
            InitializeComponent();
        }
        
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
        
        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectEmployee();
        }
        
        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            SelectEmployee();
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            ParentViewModel?.SetBlackoutMode(false);
            Close();
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            ParentViewModel?.SetBlackoutMode(false);
            Close();
        }
        
        private void SelectEmployee()
        {
            if (DataContext is EmployeeWindowViewModel viewModel && viewModel.SelectedEmployee != null)
            {
                SelectedEmployee = viewModel.SelectedEmployee;
                DialogResult = true;
                ParentViewModel?.SetBlackoutMode(false);
                Close();
            }
        }
        
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null && comboBox.SelectedItem == null)
            {
                comboBox.SelectedIndex = 0;
            }
        }
    }
}