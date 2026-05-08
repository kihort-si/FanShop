using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FanShop.Utils;

public partial class TabItem : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private UserControl _content = new();

    [ObservableProperty]
    private bool _isClosable = true;
}
