using FanShop.PriceTags.Services;
using FanShop.ReportAdjustment.Services;
using FanShop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FanShop.Services.ReportAdjustment;

public static class ReportAdjustmentModule
{
    private static ServiceProvider? _provider;
    private static ReportAdjustmentViewModel? _viewModel;
    public static ReportAdjustmentViewModel ViewModel => _viewModel ??= (_provider ??= Create()).GetRequiredService<ReportAdjustmentViewModel>();
    private static ServiceProvider Create()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FanShop", "ReportAdjustment");
        var services = new ServiceCollection();
        services.AddSingleton(_ => new LocalStorage(root));
        services.AddLogging(b => b.AddProvider(new FileLoggerProvider(Path.Combine(root, "Logs"))));
        services.AddSingleton<AdjustmentSettingsService>(); services.AddSingleton<DiscountCalculationService>();
        services.AddSingleton<RowClassificationService>(); services.AddSingleton<SalesReportExcelLoader>();
        services.AddSingleton<SolutionScoringService>(); services.AddSingleton<SolutionValidator>();
        services.AddSingleton<AdjustmentSearchService>(); services.AddSingleton<ReportAdjustmentViewModel>();
        return services.BuildServiceProvider();
    }
    public static async Task ShutdownAsync()
    {
        if (_viewModel is not null) await _viewModel.ShutdownAsync();
        if (_provider is not null) await _provider.DisposeAsync();
        _provider = null; _viewModel = null;
    }
}
