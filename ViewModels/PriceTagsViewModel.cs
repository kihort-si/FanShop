using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.PriceTags.Models;
using FanShop.PriceTags.Services;
using FanShop.Windows;
using Microsoft.Extensions.Logging;

namespace FanShop.ViewModels;

public partial class PriceTagsViewModel : ObservableObject
{
    private readonly ProductCatalogService _catalog;
    private readonly ExcelCatalogLoader _loader;
    private readonly ScanSessionService _session;
    private readonly TxtImportService _txt;
    private readonly SettingsService _settings;
    private readonly NetworkAddressService _network;
    private readonly BarcodeWebServer _server;
    private readonly TransferQueueService _transfer;
    private readonly GlobalTransferHotkeys _hotkeys;
    private readonly ILogger<PriceTagsViewModel> _logger;
    private ScanRecord[]? _saved;
    private string? _excelPath;
    private Task? _initializationTask;
    private int _refreshQueued;
    private readonly CancellationTokenSource _lifetime = new();

    [ObservableProperty] private string _catalogStatus = "Загрузите выгрузку .xlsx из 1С.";
    [ObservableProperty] private string _serverStatus = "Сервер остановлен.";
    [ObservableProperty] private string _status = "Откройте справочник и начните сканировать.";
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _hasUnknown;
    [ObservableProperty] private string _unknownMessage = "";
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private bool _recoveryAvailable;
    [ObservableProperty] private bool _unknownOnly;
    [ObservableProperty] private int _port = 8765;
    [ObservableProperty] private int _articleTextDelay = 600;
    [ObservableProperty] private int _articleEnterDelay = 400;
    [ObservableProperty] private int _characteristicTextDelay = 500;
    [ObservableProperty] private int _characteristicEnterDelay = 350;
    [ObservableProperty] private int _quantityDelay = 400;
    [ObservableProperty] private NetworkAddress? _selectedAddress;
    [ObservableProperty] private ScanRecord? _selectedScan;
    [ObservableProperty] private string _statistics = "Сканирований: 0 · Позиций: 0 · Найдено: 0 · Неизвестных: 0";
    [ObservableProperty] private string _transferMessage = "Передача доступна только в Windows.";
    [ObservableProperty] private string _progress = "Передано: 0 / 0";
    [ObservableProperty] private bool _hasQueue;
    [ObservableProperty] private bool _needsReview;
    [ObservableProperty] private bool _canEdit = true;
    [ObservableProperty] private bool _canPrepare;
    [ObservableProperty] private bool _canManageQueue;
    [ObservableProperty] private bool _canConfigure;
    [ObservableProperty] private bool _canResolveRecovery;
    [ObservableProperty] private bool _hotkeysArmed;
    [ObservableProperty] private string _currentPosition = "";
    public ObservableCollection<AggregatedProduct> Products { get; } = [];
    public ObservableCollection<ScanRecord> History { get; } = [];
    public ObservableCollection<NetworkAddress> Addresses { get; } = [];
    public ObservableCollection<TransferItem> Queue { get; } = [];
    public ObservableCollection<string> CatalogWarnings { get; } = [];
    public string DataDirectory { get; }
    public string LocalUrl => $"http://localhost:{(_server.IsRunning ? _server.Port : Port)}";

    public PriceTagsViewModel(ProductCatalogService catalog, ExcelCatalogLoader loader, ScanSessionService session,
        TxtImportService txt, SettingsService settings, NetworkAddressService network, BarcodeWebServer server,
        TransferQueueService transfer, GlobalTransferHotkeys hotkeys, LocalStorage storage, ILogger<PriceTagsViewModel> logger)
    {
        _catalog = catalog; _loader = loader; _session = session; _txt = txt; _settings = settings; _network = network;
        _server = server; _transfer = transfer; _hotkeys = hotkeys; _logger = logger; DataDirectory = storage.DirectoryPath;
        _session.Changed += ScheduleRefresh; _transfer.Changed += ScheduleRefresh;
        _hotkeys.StartRequested += StartFromHotkey;
        _hotkeys.PauseRequested += _transfer.Pause;
        _hotkeys.StopRequested += _transfer.Stop;
    }
    public Task InitializeAsync() => _initializationTask ??= InitializeCoreAsync();
    private async Task InitializeCoreAsync()
    {
        Busy = true; Refresh();
        try
        {
            var settings = await Task.Run(_settings.Load);
            Port = settings.Port; _excelPath = settings.ExcelPath;
            var delays = settings.Delays ?? new(); ArticleTextDelay = delays.ArticleText; ArticleEnterDelay = delays.ArticleEnter;
            CharacteristicTextDelay = delays.CharacteristicText; CharacteristicEnterDelay = delays.CharacteristicEnter; QuantityDelay = delays.QuantityEnter;
            _saved = await Task.Run(_session.ReadSaved);
            RecoveryAvailable = _saved is { Length: > 0 }; _session.SetRecoveryPending(RecoveryAvailable);
            if (_excelPath is not null && File.Exists(_excelPath))
            {
                try { var result = await _loader.LoadAsync(_excelPath, _lifetime.Token); _catalog.Replace(result.Products); ShowCatalog(_excelPath, result); }
                catch (Exception ex) { Report(ex, "Не удалось восстановить Excel. Загрузите справочник заново."); }
            }
            await Task.Run(_transfer.Restore);
            if (RecoveryAvailable) Status = "Есть сохранённая сессия. Выберите «Восстановить» или «Удалить сохранённую».";
            try { await _server.RestartAsync(Port, _lifetime.Token); UpdateAddresses(); }
            catch (Exception ex) { Report(ex); ServerStatus = "Сервер остановлен — доступен импорт TXT."; }
        }
        catch (Exception ex) { _session.SetRecoveryPending(true); Report(ex, "Не удалось прочитать сохранённое состояние. Проверьте JSON-файлы в каталоге данных; они не перезаписаны."); }
        finally { Busy = false; Refresh(); }
    }
    partial void OnUnknownOnlyChanged(bool value) => Refresh();
    partial void OnBusyChanged(bool value) => Refresh();
    private void StartFromHotkey() => _ = StartFromHotkeyAsync();
    private async Task StartFromHotkeyAsync()
    {
        try { await _transfer.RunAsync(GetDelays()); }
        catch (Exception ex) { await Dispatcher.UIThread.InvokeAsync(() => Report(ex)); }
    }
    private TransferDelays GetDelays() => new(ArticleTextDelay, ArticleEnterDelay, CharacteristicTextDelay, CharacteristicEnterDelay, QuantityDelay);
    private PriceTagSettings GetSettings() => new(_excelPath, Port, GetDelays());
    [RelayCommand]
    private async Task LoadExcel()
    {
        var path = await PickFile("Загрузить справочник 1С", "*.xlsx"); if (path is null) return;
        await ImportExcelAsync(path);
    }
    public async Task ImportExcelAsync(string path)
    {
        if (!CanEdit) return;
        Busy = true;
        try
        {
            var result = await _loader.LoadAsync(path, _lifetime.Token);
            await Task.Run(() => _session.ReplaceCatalog(result.Products));
            _excelPath = path; ShowCatalog(path, result); await Task.Run(() => _settings.Save(GetSettings()));
            Success($"Загружено {result.Products.Count} штрихкодов. Пропущено строк: {result.Warnings.Count}.");
        }
        catch (Exception ex) { Report(ex); }
        finally { Busy = false; }
    }
    private void ShowCatalog(string path, CatalogLoadResult result)
    {
        CatalogStatus = $"{Path.GetFileName(path)} · {_catalog.Count} штрихкодов";
        CatalogWarnings.Clear(); foreach (var warning in result.Warnings) CatalogWarnings.Add(warning);
    }
    [RelayCommand]
    private async Task ImportTxt()
    {
        var path = await PickFile("Импортировать сканирования TXT", "*.txt"); if (path is not null) await ImportTxtAsync(path);
    }
    public async Task ImportTxtAsync(string path)
    {
        if (!CanEdit) return;
        Busy = true; var before = _session.Snapshot().Length;
        try { var count = await _txt.ImportAsync(path, _lifetime.Token); Success($"Из TXT добавлено {count} сканирований. Пропущено некорректных строк: {_txt.Warnings.Count}. " + string.Join(" ", _txt.Warnings.Take(3))); }
        catch (Exception ex) { Report(ex, $"Импорт прерван. Добавлено {_session.Snapshot().Length - before} строк; проверьте историю перед повторным импортом."); }
        finally { Busy = false; Refresh(); }
    }
    [RelayCommand]
    private async Task RestoreSession()
    {
        Busy = true;
        try { await Task.Run(() => _session.Restore(_saved ?? [])); RecoveryAvailable = false; _saved = null; Success("Сессия восстановлена. Результаты поиска сохранены на момент сканирования."); }
        catch (Exception ex) { Report(ex); }
        finally { Busy = false; Refresh(); }
    }
    [RelayCommand]
    private async Task DiscardSaved()
    {
        if (!await Confirm("Удалить сохранённую сессию и её очередь передачи? Это действие нельзя отменить.")) return;
        Busy = true;
        try { await Task.Run(() => { _transfer.CloseQueue(); _session.DiscardSaved(); }); _hotkeys.Disarm(); RecoveryAvailable = false; _saved = null; Success("Сохранённая сессия удалена."); }
        catch (Exception ex) { Report(ex); }
        finally { Busy = false; Refresh(); }
    }
    [RelayCommand]
    private async Task Undo()
    {
        try { await Task.Run(() => _session.Undo()); Success("Последнее сканирование отменено."); } catch (Exception ex) { Report(ex); }
    }
    [RelayCommand]
    private async Task RemoveSelected()
    {
        if (SelectedScan is not { } scan) return;
        try { await Task.Run(() => _session.Remove(scan.Id)); Success($"Удалено сканирование {scan.Barcode}."); } catch (Exception ex) { Report(ex); }
    }
    [RelayCommand]
    private async Task Clear()
    {
        if (!await Confirm("Очистить все сканирования текущей сессии? Это действие нельзя отменить.")) return;
        try { await Task.Run(_session.Clear); Success("Сессия очищена."); } catch (Exception ex) { Report(ex); }
    }
    [RelayCommand]
    private async Task RestartServer()
    {
        Busy = true;
        try { await Task.Run(() => _settings.Save(GetSettings())); await _server.RestartAsync(Port, _lifetime.Token); UpdateAddresses(); Success("Сервер запущен. Откройте адрес на кассе."); }
        catch (Exception ex) { Report(ex); ServerStatus = _server.IsRunning ? $"Сервер работает на порту {_server.Port}." : "Сервер остановлен."; }
        finally { Busy = false; }
    }
    [RelayCommand]
    private async Task StopServer()
    {
        try { await _server.StopAsync(); ServerStatus = "Сервер остановлен."; } catch (Exception ex) { Report(ex); }
    }
    [RelayCommand]
    private void RefreshAddresses()
    {
        try { UpdateAddresses(); } catch (Exception ex) { Report(ex); }
    }
    private void UpdateAddresses()
    {
        Addresses.Clear(); foreach (var address in _network.GetAddresses(_server.IsRunning ? _server.Port : Port)) Addresses.Add(address);
        SelectedAddress = Addresses.FirstOrDefault();
        ServerStatus = _server.IsRunning ? $"Сервер запущен · порт {_server.Port}" : "Сервер остановлен.";
        if (Addresses.Count == 0) ServerStatus += " Подходящий сетевой интерфейс не найден. Проверьте Ethernet/Wi-Fi или используйте TXT.";
        OnPropertyChanged(nameof(LocalUrl));
    }
    [RelayCommand]
    private async Task CopyAddress()
    {
        try
        {
            if (SelectedAddress is null) throw new InvalidOperationException("Выберите сетевой адрес. Для проверки на этом компьютере используйте localhost.");
            var clipboard = Owner()?.Clipboard ?? throw new InvalidOperationException("Буфер обмена недоступен.");
            await clipboard.SetTextAsync(SelectedAddress.Url); Success("Адрес скопирован.");
        }
        catch (Exception ex) { Report(ex); }
    }
    [RelayCommand]
    private async Task SaveDelays()
    {
        try { await Task.Run(() => _settings.Save(GetSettings())); Success("Настройки сохранены."); } catch (Exception ex) { Report(ex); }
    }
    [RelayCommand]
    private async Task PrepareTransfer()
    {
        if (!CanPrepare) return;
        Busy = true;
        try { GetDelays().Validate(); await _hotkeys.ArmAsync(); await Task.Run(_transfer.Prepare); Success("Очередь сформирована. Выполните инструкцию и нажмите F8 в 1С."); }
        catch (Exception ex) { _hotkeys.Disarm(); Report(ex); }
        finally { Busy = false; Refresh(); }
    }
    [RelayCommand]
    private async Task ArmHotkeys()
    {
        try { await _hotkeys.ArmAsync(); Success("F8/F9/Esc включены. Начните передачу клавишей F8 в окне 1С."); }
        catch (Exception ex) { Report(ex); }
        Refresh();
    }
    [RelayCommand] private void PauseTransfer() => _transfer.Pause();
    [RelayCommand] private void StopTransfer() => _transfer.Stop();
    [RelayCommand]
    private async Task ReviewCompleted()
    {
        if (!await Confirm("Вы проверили строку в 1С: артикул, характеристика и количество полностью правильные? Подтвердить её как переданную?")) return;
        try { await Task.Run(() => _transfer.ResolveReview(true)); } catch (Exception ex) { Report(ex); }
    }
    [RelayCommand]
    private async Task ReviewRetry()
    {
        if (!await Confirm("Удалите частично введённую строку в 1С и подготовьте пустую строку с курсором в «Номенклатура». Разрешить повтор этой позиции?")) return;
        try { await Task.Run(() => _transfer.ResolveReview(false)); } catch (Exception ex) { Report(ex); }
    }
    [RelayCommand]
    private async Task CloseQueue()
    {
        if (!await Confirm("Закрыть очередь передачи? Сканирования останутся. Новая очередь будет содержать всю сессию, включая уже переданные позиции. Для новой партии очистите сессию.")) return;
        try { await Task.Run(_transfer.CloseQueue); _hotkeys.Disarm(); Refresh(); } catch (Exception ex) { Report(ex); }
    }
    private void ScheduleRefresh()
    {
        if (Interlocked.Exchange(ref _refreshQueued, 1) != 0) return;
        Dispatcher.UIThread.Post(() => { Interlocked.Exchange(ref _refreshQueued, 0); Refresh(); });
    }
    private void Refresh()
    {
        var records = _session.Snapshot(); var aggregates = ScanSessionService.Aggregate(records); var items = _transfer.Snapshot();
        var selectedId = SelectedScan?.Id;
        Replace(Products, aggregates); Replace(History, records.Reverse().Where(r => !UnknownOnly || !r.Found)); Replace(Queue, items);
        SelectedScan = History.FirstOrDefault(r => r.Id == selectedId);
        Statistics = $"Сканирований: {records.Length} · Позиций: {aggregates.Length} · Найдено: {records.Count(r => r.Found)} · Неизвестных: {records.Count(r => !r.Found)}";
        HasQueue = items.Length > 0; NeedsReview = items.Any(i => i.Status is TransferStatus.Interrupted or TransferStatus.Failed);
        CanConfigure = !Busy && !_transfer.Running && !RecoveryAvailable;
        CanResolveRecovery = RecoveryAvailable && !Busy;
        CanEdit = !Busy && !_session.IsFrozen; CanManageQueue = HasQueue && !_transfer.Running && !Busy && !RecoveryAvailable;
        CanPrepare = CanEdit && _transfer.IsSupported && _catalog.Count > 0 && records.Length > 0 && records.All(r => r.Found) && !HasQueue;
        HotkeysArmed = _hotkeys.Armed;
        TransferMessage = _transfer.IsSupported ? _transfer.Message : "Передача в 1С доступна только в Windows. Импорт и сетевое сканирование работают на этой ОС.";
        Progress = $"Передано: {items.Count(i => i.Status == TransferStatus.Completed)} / {items.Length}";
        var current = items.FirstOrDefault(i => i.Status is TransferStatus.Sending or TransferStatus.Interrupted or TransferStatus.Failed);
        CurrentPosition = current is null ? "" : $"{current.Article} / {current.Characteristic} / {current.Quantity}";
        var unknown = records.LastOrDefault(r => !r.Found);
        HasUnknown = unknown is not null;
        UnknownMessage = unknown is null ? "" : $"Штрихкод {unknown.Barcode} не найден в загруженном справочнике. Неизвестных сканирований: {records.Count(r => !r.Found)}. Удалите их в истории перед передачей.";
    }
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    { target.Clear(); foreach (var item in source) target.Add(item); }
    private void Success(string message) { HasError = false; Status = message; }
    private void Report(Exception ex, string? prefix = null)
    { HasError = true; Status = $"{prefix} {ex.Message}".Trim(); _logger.LogError(ex, "{Message}", prefix ?? "Ошибка раздела ценников"); }
    private static Window? Owner() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    private static async Task<string?> PickFile(string title, string pattern)
    {
        var owner = Owner(); if (owner is null) return null;
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = title, AllowMultiple = false, FileTypeFilter = [new FilePickerFileType(pattern) { Patterns = [pattern] }] });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
    private static async Task<bool> Confirm(string message)
    {
        var owner = Owner(); if (owner is null) return false;
        var dialog = new ConfirmDialog { DataContext = new ConfirmDialogViewModel { Message = message } };
        return await dialog.ShowDialog<bool>(owner);
    }
    public async Task ShutdownAsync()
    {
        _lifetime.Cancel(); _transfer.Stop();
        if (_initializationTask is not null) await _initializationTask;
        for (var i = 0; i < 100 && _transfer.Running; i++) await Task.Delay(20);
        _hotkeys.Disarm(); await _server.StopAsync();
        _session.Changed -= ScheduleRefresh; _transfer.Changed -= ScheduleRefresh;
        _hotkeys.StartRequested -= StartFromHotkey; _hotkeys.PauseRequested -= _transfer.Pause; _hotkeys.StopRequested -= _transfer.Stop;
        _lifetime.Dispose();
    }
}
