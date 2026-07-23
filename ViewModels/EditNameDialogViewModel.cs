using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FanShop.ViewModels;

public partial class EditNameDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [RelayCommand]
    private void Save(Window? window)
    {
        window?.Close(true);
    }

    [RelayCommand]
    private void Cancel(Window? window)
    {
        window?.Close(false);
    }
}