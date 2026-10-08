using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using FanShop.PriceTags.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FanShop.PriceTags.Services;

public sealed class NetworkAddressService
{
    public NetworkAddress[] GetAddresses(int port) => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
        .Where(n => !new[] { "virtual", "vmware", "vbox", "virtualbox", "hyper-v", "vpn", "tunnel", "vethernet", "docker", "tailscale", "wireguard" }
            .Any(s => (n.Name + " " + n.Description).Contains(s, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address)
                && !a.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
            .Select(a => new NetworkAddress(n.Name, $"http://{a.Address}:{port}"))).ToArray();
}
public sealed record ScanRequest(string? Barcode, Guid RequestId);
public sealed class BarcodeWebServer(ScanSessionService session, ProductCatalogService catalog, ILogger<BarcodeWebServer> logger) : IAsyncDisposable
{
    private WebApplication? _app;
    private readonly SemaphoreSlim _lifecycle = new(1);
    public bool IsRunning => _app is not null;
    public int Port { get; private set; }
    public async Task RestartAsync(int port, CancellationToken token = default)
    {
        if (port is < 1024 or > 65535) throw new InvalidOperationException("Порт должен быть от 1024 до 65535.");
        await _lifecycle.WaitAsync(token);
        try
        {
            await StopCoreAsync();
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(k => { k.Listen(IPAddress.Any, port); k.Limits.MaxRequestBodySize = 4096; });
            var app = builder.Build();
            app.Use(async (context, next) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'";
                if (context.Request.Method == "POST" && context.Request.Headers.Origin.Count > 0
                    && (!Uri.TryCreate(context.Request.Headers.Origin.ToString(), UriKind.Absolute, out var origin)
                        || origin.Authority != context.Request.Host.Value))
                { context.Response.StatusCode = 403; return; }
                await next(context);
            });
            foreach (var asset in new[] { ("/", "index.html", "text/html; charset=utf-8"), ("/app.js", "app.js", "text/javascript; charset=utf-8"), ("/styles.css", "styles.css", "text/css; charset=utf-8") })
            {
                var content = ReadAsset(asset.Item2);
                app.MapGet(asset.Item1, () => Results.Text(content, asset.Item3));
            }
            app.MapGet("/api/status", () => Results.Json(new { ready = catalog.Count > 0 && !session.IsFrozen, catalogCount = catalog.Count,
                message = session.IsFrozen ? "На ноутбуке открыта очередь передачи или ожидается восстановление." : catalog.Count == 0 ? "Загрузите Excel на ноутбуке." : "Готов к сканированию" }));
            app.MapPost("/api/scan", (ScanRequest request) =>
            {
                try
                {
                    if (request.RequestId == Guid.Empty) return Results.BadRequest(new { error = "Отсутствует идентификатор сканирования." });
                    var record = session.ProcessBarcode(request.Barcode ?? "", ScanSource.Network, request.RequestId);
                    return Results.Json(new { record.Id, record.Barcode, record.Found, record.Product,
                        message = record.Found ? "Товар найден" : $"Штрихкод {record.Barcode} не найден в загруженном справочнике." });
                }
                catch (InvalidDataException ex) { return Results.BadRequest(new { error = ex.Message }); }
                catch (InvalidOperationException ex) { return Results.Json(new { error = ex.Message }, statusCode: 409); }
                catch (Exception ex)
                { logger.LogError(ex, "Не удалось сохранить сетевой скан"); return Results.Json(new { error = "Не удалось сохранить сканирование на ноутбуке. Проверьте свободное место и журнал." }, statusCode: 503); }
            });
            try { await app.StartAsync(token); _app = app; Port = port; logger.LogInformation("HTTP-сервер запущен, порт {Port}", port); }
            catch (Exception ex)
            { await app.DisposeAsync(); logger.LogError(ex, "Не удалось запустить HTTP-сервер {Port}", port); throw new IOException($"Не удалось запустить локальный сервер на порту {port}. Возможно, порт уже используется.", ex); }
        }
        finally { _lifecycle.Release(); }
    }
    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync();
        try { await StopCoreAsync(); } finally { _lifecycle.Release(); }
    }
    private async Task StopCoreAsync()
    {
        if (_app is null) return;
        var app = _app; _app = null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await app.StopAsync(timeout.Token); } finally { await app.DisposeAsync(); }
        logger.LogInformation("HTTP-сервер остановлен");
    }
    private static string ReadAsset(string name)
    {
        var assembly = typeof(BarcodeWebServer).Assembly;
        using var stream = assembly.GetManifestResourceStream($"FanShop.PriceTags.Core.Web.{name}")
            ?? throw new FileNotFoundException($"Не найден ресурс web-страницы: {name}");
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    public ValueTask DisposeAsync() => new(StopAsync());
}
