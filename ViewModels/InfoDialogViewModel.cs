using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FanShop.ViewModels;

public partial class InfoDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = string.Empty;

    [RelayCommand]
    private void Close(Window? window)
    {
        window?.Close();
    }
}