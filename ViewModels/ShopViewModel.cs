using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;
using FanShop.Services;

namespace FanShop.ViewModels;

public partial class ShopViewModel : ObservableObject
{
    public int ShopID { get; set; }

    [ObservableProperty]
    private string _shopName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DefaultLabel))]
    private bool _isDefault;

    public string DefaultLabel => IsDefault ? "По умолчанию" : "Сделать основным";

    [ObservableProperty]
    private ObservableCollection<PositionViewModel> _positions = new();

    [RelayCommand]
    private void AddPosition()
    {
        using var context = new AppDbContext();

        var position = new Position
        {
            ShopID = ShopID,
            PositionName = "Новая должность",
            IsDefault = !context.Positions.Any(item => item.ShopID == ShopID)
        };

        context.Positions.Add(position);
        context.SaveChanges();

        context.SalaryHistories.Add(new SalaryHistory
        {
            PositionID = position.PositionID,
            Salary = 0,
            StartDate = DateTime.Today
        });

        context.SaveChanges();

        Positions.Add(new PositionViewModel
        {
            PositionID = position.PositionID,
            PositionName = position.PositionName,
            CurrentSalary = 0,
            IsDefault = position.IsDefault,
            Shop = this
        });
        WorkplaceCatalogNotifier.NotifyChanged();
    }

    [RelayCommand]
    private void RemovePosition(PositionViewModel? positionVm)
    {
        if (positionVm == null)
            return;

        using var context = new AppDbContext();

        var position = context.Positions
            .FirstOrDefault(x => x.PositionID == positionVm.PositionID);

        if (position == null)
            return;

        context.Positions.Remove(position);
        context.SaveChanges();

        Positions.Remove(positionVm);
        WorkplaceCatalogNotifier.NotifyChanged();
    }
}
