using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FanShop.ViewModels;

public partial class PositionViewModel : ObservableObject
{
    public int PositionID { get; set; }

    [ObservableProperty]
    private string _positionName = string.Empty;

    [ObservableProperty]
    private decimal _currentSalary;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DefaultLabel))]
    private bool _isDefault;

    public string DefaultLabel => IsDefault ? "По умолчанию" : "Сделать основной";

    public ShopViewModel? Shop { get; set; }
}
