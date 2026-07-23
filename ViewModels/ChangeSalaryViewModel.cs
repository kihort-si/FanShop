using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FanShop.ViewModels;

public partial class ChangeSalaryViewModel : ObservableObject
{
    [ObservableProperty]
    private decimal _newSalary;

    [ObservableProperty]
    private DateTimeOffset _startDate = DateTimeOffset.Now.Date;

    [RelayCommand]
    private void Confirm(Window? window)
    {
        window?.Close(true);
    }

    [RelayCommand]
    private void Cancel(Window? window)
    {
        window?.Close(false);
    }
}