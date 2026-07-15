using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FanShop.ViewModels;

public partial class ConfirmDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = string.Empty;

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