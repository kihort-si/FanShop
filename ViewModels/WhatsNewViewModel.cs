using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class WhatsNewViewModel : ObservableObject
{
    
    [ObservableProperty]
    private string _version = "";
    
    [ObservableProperty]
    private ObservableCollection<WhatsNewSection> _sections = [];

    [RelayCommand]
    private void Close(Window window)
    {
        window.Close();
    }
}

public class WhatsNewSection
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}