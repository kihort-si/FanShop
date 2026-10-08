using FanShop.PriceTags.Services;
using FanShop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using FanShop.PriceTags.Models;

namespace FanShop.Services.PriceTags;

public static class PriceTagModule
{
    private static ServiceProvider? _provider;
    private static PriceTagsViewModel? _viewModel;
    public static PriceTagsViewModel ViewModel => _viewModel ??= (_provider ??= Create()).GetRequiredService<PriceTagsViewModel>();
    private static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FanShop", "PriceTags");
    public static Task<bool> HasSavedWorkAsync() => Task.Run(() =>
    {
        try
        {
            var sessionPath = Path.Combine(DataDirectory, "session.json");
            var queuePath = Path.Combine(DataDirectory, "transfer.json");
            return (File.Exists(sessionPath) && JsonSerializer.Deserialize<PersistedSession>(File.ReadAllText(sessionPath)) is { } saved && (saved.Records is null || saved.Records.Length > 0))
                || (File.Exists(queuePath) && JsonSerializer.Deserialize<TransferItem[]>(File.ReadAllText(queuePath)) is { Length: > 0 });
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return true; }
    });
    private static ServiceProvider Create()
    {
        var root = DataDirectory;
        var services = new ServiceCollection();
        services.AddSingleton(_ => new LocalStorage(root));
        services.AddLogging(b => b.AddProvider(new FileLoggerProvider(Path.Combine(root, "Logs"))).SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<ProductCatalogService>(); services.AddSingleton<ExcelCatalogLoader>();
        services.AddSingleton<ScanSessionService>(); services.AddSingleton<TxtImportService>();
        services.AddSingleton<SettingsService>(); services.AddSingleton<NetworkAddressService>();
        services.AddSingleton<BarcodeWebServer>(); services.AddSingleton<IOneCKeyboard, WindowsOneCKeyboard>();
        services.AddSingleton<TransferQueueService>(); services.AddSingleton<GlobalTransferHotkeys>();
        services.AddSingleton<PriceTagsViewModel>();
        return services.BuildServiceProvider();
    }
    public static async Task ShutdownAsync()
    {
        if (_provider is null) return;
        if (_viewModel is not null) await _viewModel.ShutdownAsync();
        await _provider.DisposeAsync(); _provider = null; _viewModel = null;
    }
}
