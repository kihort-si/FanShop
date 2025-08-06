using System.Windows;
using FanShop.ViewModels;

namespace FanShop.Windows;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            viewModel.CloseRequested += () => Close();
        }
    }
}