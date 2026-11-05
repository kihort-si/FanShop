using FanShop.Replenishment;
using FanShop.ViewModels;

namespace FanShop.Services.Replenishment;

public static class ReplenishmentModule
{
    private static ReplenishmentViewModel? _viewModel;
    public static ReplenishmentViewModel ViewModel => _viewModel ??= new(new ReplenishmentStore(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FanShop", "Replenishment")),
        () => FanShop.Services.PriceTags.PriceTagModule.ViewModel.EnsureCanAcceptReplenishmentAsync(),
        (batch, day) => FanShop.Services.PriceTags.PriceTagModule.ViewModel.AcceptReplenishmentAsync(batch, day),
        FanShop.Services.PriceTags.PriceTagModule.HasBatchInQueue,
        async () =>
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow?.DataContext is MainWindowViewModel main)
                await main.OpenPriceTagsTabCommand.ExecuteAsync(null);
        });
    public static async Task ShutdownAsync()
    {
        if (_viewModel is null) return;
        await _viewModel.ShutdownAsync(); _viewModel = null;
    }
}
