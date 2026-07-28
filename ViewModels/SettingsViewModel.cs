using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Models;
using FanShop.Services;
using FanShop.Windows;

namespace FanShop.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly Settings _settings;

    [ObservableProperty]
    private string _head = string.Empty;

    [ObservableProperty]
    private string _responsiblePerson = string.Empty;

    [ObservableProperty]
    private string _responsiblePhoneNumber = string.Empty;

    [ObservableProperty]
    private string _responsiblePosition = string.Empty;

    [ObservableProperty]
    private string _visitGoal = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<ShopViewModel> _shops = new();

    public SettingsViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _settings = Settings.Load();
        LoadShops();

        Head = _settings.Head;
        ResponsiblePerson = _settings.ResponsiblePerson;
        ResponsiblePhoneNumber = _settings.ResponsiblePhoneNumber;
        ResponsiblePosition = _settings.ResponsiblePosition;
        VisitGoal = _settings.VisitGoal;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.Head = Head;
        _settings.ResponsiblePerson = ResponsiblePerson;
        _settings.ResponsiblePhoneNumber = ResponsiblePhoneNumber;
        _settings.ResponsiblePosition = ResponsiblePosition;
        _settings.VisitGoal = VisitGoal;
        _settings.Save();
        SaveShops();

        _mainWindowViewModel.RefreshStatistics();
        _mainWindowViewModel.CloseTabRequest(this);
    }

    [RelayCommand]
    private void Cancel()
    {
        _mainWindowViewModel.CloseTabRequest(this);
    }
    
    private void LoadShops()
    {
        using var context = new AppDbContext();

        var shops = context.Shops.OrderByDescending(shop => shop.IsDefault).ThenBy(shop => shop.ShopName).ToList();

        Shops.Clear();

        foreach (var shop in shops)
        {
            var shopVm = new ShopViewModel
            {
                ShopID = shop.ShopID,
                ShopName = shop.ShopName,
                IsDefault = shop.IsDefault
            };

            var positions = context.Positions
                .Where(p => p.ShopID == shop.ShopID)
                .OrderByDescending(position => position.IsDefault)
                .ThenBy(position => position.PositionName)
                .ToList();

            foreach (var position in positions)
            {
                var currentSalary = context.SalaryHistories
                    .Where(x =>
                        x.PositionID == position.PositionID &&
                        x.EndDate == null)
                    .Select(x => x.Salary)
                    .FirstOrDefault();

                var positionVm = new PositionViewModel
                {
                    PositionID = position.PositionID,
                    PositionName = position.PositionName,
                    CurrentSalary = currentSalary,
                    IsDefault = position.IsDefault,
                    Shop = shopVm
                };

                shopVm.Positions.Add(positionVm);
            }

            Shops.Add(shopVm);
        }
    }
    
    private void SaveShops()
    {
        using var context = new AppDbContext();

        foreach (var shopVm in Shops)
        {
            var shop = context.Shops
                .FirstOrDefault(x => x.ShopID == shopVm.ShopID);

            if (shop != null)
            {
                shop.ShopName = shopVm.ShopName;
                shop.IsDefault = shopVm.IsDefault;
            }

            foreach (var positionVm in shopVm.Positions)
            {
                var position = context.Positions
                    .FirstOrDefault(x => x.PositionID == positionVm.PositionID);

                if (position != null)
                {
                    position.PositionName = positionVm.PositionName;
                    position.IsDefault = positionVm.IsDefault;
                }
            }
        }

        context.SaveChanges();
    }
    
    [RelayCommand]
    private void AddShop()
    {
        using var context = new AppDbContext();

        var shop = new Shop
        {
            ShopName = "Новый магазин",
            OpenDate = DateTime.Today,
            IsDefault = !context.Shops.Any()
        };

        context.Shops.Add(shop);
        context.SaveChanges();

        Shops.Add(new ShopViewModel
        {
            ShopID = shop.ShopID,
            ShopName = shop.ShopName,
            IsDefault = shop.IsDefault
        });
    }

    [RelayCommand]
    private void SetDefaultShop(ShopViewModel? selectedShop)
    {
        if (selectedShop == null)
            return;

        using var context = new AppDbContext();
        foreach (var shop in context.Shops)
            shop.IsDefault = shop.ShopID == selectedShop.ShopID;
        context.SaveChanges();

        foreach (var shop in Shops)
            shop.IsDefault = shop.ShopID == selectedShop.ShopID;
    }

    [RelayCommand]
    private void SetDefaultPosition(PositionViewModel? selectedPosition)
    {
        if (selectedPosition?.Shop == null)
            return;

        using var context = new AppDbContext();
        var positions = context.Positions.Where(position => position.ShopID == selectedPosition.Shop.ShopID).ToList();
        foreach (var position in positions)
            position.IsDefault = position.PositionID == selectedPosition.PositionID;
        context.SaveChanges();

        foreach (var position in selectedPosition.Shop.Positions)
            position.IsDefault = position.PositionID == selectedPosition.PositionID;
    }
    
    [RelayCommand]
    private async Task ChangeSalary(PositionViewModel? position)
    {
        if (position == null)
            return;

        var vm = new ChangeSalaryViewModel
        {
            NewSalary = position.CurrentSalary
        };

        var window = new ChangeSalaryWindow
        {
            DataContext = vm
        };

        var owner = GetMainWindow();

        if (owner == null)
            return;

        var result = await window.ShowDialog<bool>(owner);

        if (!result)
            return;

        using var context = new AppDbContext();

        var salaryService = new SalaryService(context);

        var managementService = new SalaryManagementService(
            context,
            salaryService);

        managementService.ChangeSalary(
            position.PositionID,
            vm.NewSalary,
            vm.StartDate.Date);

        position.CurrentSalary = vm.NewSalary;
        
        _mainWindowViewModel.RefreshStatistics();
    }
    
    [RelayCommand]
    private async Task EditPositionName(PositionViewModel? positionVm)
    {
        if (positionVm == null)
            return;

        var vm = new EditNameDialogViewModel
        {
            Title = "Изменение названия должности",
            Name = positionVm.PositionName
        };

        var dialog = new EditNameDialog
        {
            DataContext = vm
        };

        var owner = GetMainWindow();

        if (owner == null)
            return;

        var result = await dialog.ShowDialog<bool>(owner);

        if (!result)
            return;

        using var context = new AppDbContext();

        var position = context.Positions
            .FirstOrDefault(x => x.PositionID == positionVm.PositionID);

        if (position == null)
            return;

        position.PositionName = vm.Name;

        context.SaveChanges();

        positionVm.PositionName = vm.Name;
    }
    
    [RelayCommand]
    private async Task DeletePosition(PositionViewModel? positionVm)
    {
        if (positionVm == null)
            return;

        using var context = new AppDbContext();

        bool hasShifts = context.WorkDayEmployee
            .Any(x => x.PositionID == positionVm.PositionID);

        if (hasShifts)
        {
            await DialogService.ShowInfo(
                "Невозможно удалить должность.\n\n" +
                "По данной должности уже существуют смены.");

            return;
        }

        var dialog = new ConfirmDialog
        {
            DataContext = new ConfirmDialogViewModel
            {
                Message =
                    $"Вы действительно хотите удалить должность?\n\n" +
                    $"{positionVm.PositionName}"
            }
        };

        var owner = GetMainWindow();

        if (owner == null)
            return;

        var confirmed = await dialog.ShowDialog<bool>(owner);

        if (!confirmed)
            return;

        var position = context.Positions
            .FirstOrDefault(x => x.PositionID == positionVm.PositionID);

        if (position == null)
            return;

        context.Positions.Remove(position);

        context.SaveChanges();

        var shop = Shops
            .FirstOrDefault(s => s.Positions.Contains(positionVm));

        shop?.Positions.Remove(positionVm);
    }
    
    [RelayCommand]
    private async Task EditShopName(ShopViewModel? shopVm)
    {
        if (shopVm == null)
            return;

        var vm = new EditNameDialogViewModel
        {
            Title = "Изменение названия магазина",
            Name = shopVm.ShopName
        };

        var dialog = new EditNameDialog
        {
            DataContext = vm
        };

        var owner = GetMainWindow();

        if (owner == null)
            return;

        var result = await dialog.ShowDialog<bool>(owner);

        if (!result)
            return;

        using var context = new AppDbContext();

        var shop = context.Shops
            .FirstOrDefault(x => x.ShopID == shopVm.ShopID);

        if (shop == null)
            return;

        shop.ShopName = vm.Name;

        context.SaveChanges();

        shopVm.ShopName = vm.Name;
    }
    
    [RelayCommand]
    private async Task DeleteShop(ShopViewModel? shopVm)
    {
        if (shopVm == null)
            return;

        using var context = new AppDbContext();

        var positionIds = context.Positions
            .Where(x => x.ShopID == shopVm.ShopID)
            .Select(x => x.PositionID)
            .ToList();

        bool hasShifts = context.WorkDayEmployee
            .Any(x => positionIds.Contains(x.PositionID));

        if (hasShifts)
        {
            await DialogService.ShowInfo(
                "Невозможно удалить магазин.\n\n" +
                "По его должностям уже существуют смены.");

            return;
        }

        var dialog = new ConfirmDialog
        {
            DataContext = new ConfirmDialogViewModel
            {
                Message =
                    $"Вы действительно хотите удалить магазин?\n\n" +
                    $"{shopVm.ShopName}"
            }
        };

        var owner = GetMainWindow();

        if (owner == null)
            return;

        var confirmed = await dialog.ShowDialog<bool>(owner);

        if (!confirmed)
            return;

        var shop = context.Shops
            .FirstOrDefault(x => x.ShopID == shopVm.ShopID);

        if (shop == null)
            return;

        context.Shops.Remove(shop);

        context.SaveChanges();

        Shops.Remove(shopVm);
    }
    
    private static Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}
