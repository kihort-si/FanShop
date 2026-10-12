using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FanShop.View;
using FanShop.ViewModels;
using FanShop.PriceTags.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(FanShop.PriceTags.Tests.TestAppBuilder))]
namespace FanShop.PriceTags.Tests;
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
public class AvaloniaTests
{
    [AvaloniaFact]
    public void IntegratedViewLoadsAndLaysOutWithinExistingWindow()
    {
        var view=new PriceTagsControl();
        var window=new MainWindow { DataContext = null, Content=view, WindowState=WindowState.Normal, Width=1280, Height=820 };
        // Do not attach a production view model: this test must not touch real user files or start a production server.
        window.Show(); window.UpdateLayout();
        Assert.True(view.Bounds.Width>900); Assert.NotEmpty(view.GetVisualDescendants().OfType<DataGrid>());
        Assert.Contains(view.GetVisualDescendants().OfType<Button>(), b=>Equals(b.Content,"Передать в 1С"));
        window.CaptureRenderedFrame()?.Save(Path.Combine(Path.GetTempPath(), "fanshop-price-tags-ui.png"));
        window.Content=null; // avoid application lifecycle shutdown in the headless test
        window.Close();
    }
    [AvaloniaFact]
    public async Task ViewModelBindingsReflectFoundUnknownAndUndo()
    {
        var root=Path.Combine(Path.GetTempPath(),"fanshop-gui-"+Guid.NewGuid());
        var services=new ServiceCollection(); services.AddLogging();services.AddSingleton(_ => new LocalStorage(root));
        services.AddSingleton<ProductCatalogService>();services.AddSingleton<ExcelCatalogLoader>();services.AddSingleton<ScanSessionService>();
        services.AddSingleton<TxtImportService>();services.AddSingleton<SettingsService>();services.AddSingleton<NetworkAddressService>();services.AddSingleton<BarcodeWebServer>();
        services.AddSingleton<IOneCKeyboard,WindowsOneCKeyboard>();services.AddSingleton<TransferQueueService>();services.AddSingleton<GlobalTransferHotkeys>();services.AddSingleton<PriceTagsViewModel>();
        await using var provider=services.BuildServiceProvider();var vm=provider.GetRequiredService<PriceTagsViewModel>();
        var catalog=provider.GetRequiredService<ProductCatalogService>();catalog.Replace([new("001","ЦБ-1","Кружка","MISC")]);
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0); listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        provider.GetRequiredService<SettingsService>().Save(new(Port:port));
        await vm.InitializeAsync();
        var session=provider.GetRequiredService<ScanSessionService>();session.ProcessBarcode("001",FanShop.PriceTags.Models.ScanSource.Network);session.ProcessBarcode("999",FanShop.PriceTags.Models.ScanSource.File);
        vm.UnknownOnly=true; Assert.Single(vm.Products);Assert.Single(vm.History);Assert.Contains("Неизвестных: 1",vm.Statistics);Assert.False(vm.CanPrepare);Assert.True(vm.HasUnknown);
        session.Undo();vm.UnknownOnly=false;Assert.Single(vm.History);Assert.Contains("Неизвестных: 0",vm.Statistics);Assert.False(vm.HasUnknown);
        var view = new PriceTagsControl { DataContext = vm };
        var window = new Window { Content=view, Width=1280, Height=1000 };
        window.Show(); window.UpdateLayout();
        var portEditor = view.GetVisualDescendants().OfType<NumericUpDown>().First();
        Assert.Equal((decimal?)port, portEditor.Value);
        portEditor.Value=port+1; Assert.Equal(port+1,vm.Port);
        window.CaptureRenderedFrame()?.Save(Path.Combine(Path.GetTempPath(), "fanshop-price-tags-ui.png"));
        window.Close();
        // BarcodeWebServer is IAsyncDisposable; dispose asynchronously to avoid blocking the UI thread.
        await vm.ShutdownAsync();
        await provider.DisposeAsync();
        Directory.Delete(root,true);
    }
}
