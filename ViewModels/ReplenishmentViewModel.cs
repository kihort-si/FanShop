using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.Replenishment;

namespace FanShop.ViewModels;

public partial class ReplenishmentViewModel : ObservableObject
{
    private readonly ReplenishmentStore _store;
    private readonly InventoryReportLoader _loader = new();
    private readonly Func<Task>? _validateLabelTransfer;
    private readonly Func<ReplenishmentLabelBatch, DateOnly, Task>? _transferLabels;
    private readonly Func<Guid, bool>? _batchQueued;
    private readonly Func<Task>? _showPriceTags;
    public ObservableCollection<ReplenishmentLabelChoice> LabelChoices { get; } = [];
    public ObservableCollection<ReplenishmentLabelBatch> LabelBatches { get; } = [];
    [ObservableProperty] private ReplenishmentLabelBatch? _selectedBatch;
    [ObservableProperty] private string _labelSummary = "";
    private ReplenishmentRow[] _allRows = [];
    private TaskCompletionSource? _operation;
    private bool _stopping;
    public ObservableCollection<ReplenishmentRow> Rows { get; } = [];
    public ObservableCollection<ReplenishmentEvent> Events { get; } = [];
    public ObservableCollection<ImportedReport> Reports { get; } = [];
    public ObservableCollection<ExclusionRule> Exclusions { get; } = [];
    [ObservableProperty] private DateTimeOffset? _selectedDate = DateTimeOffset.Now.Date;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private bool _pendingOnly;
    [ObservableProperty] private ReplenishmentRow? _selectedRow;
    [ObservableProperty] private ReplenishmentEvent? _selectedEvent;
    [ObservableProperty] private ExclusionRule? _selectedExclusion;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isError;
    [ObservableProperty] private string _message = "Загрузите ведомость за один день. Повторные выгрузки за день сохраняют отметки уже довешенного.";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _ruleValue = "";
    [ObservableProperty] private int _ruleType;
    public bool CanEdit => !IsBusy;
    public bool CanComplete => CanEdit && SelectedRow?.Remaining > 0;
    public bool CanChangeBatch => CanEdit && SelectedBatch is { Printed: null, Cancelled: null };
    partial void OnSelectedBatchChanged(ReplenishmentLabelBatch? value)
    { ConfirmPrintedCommand.NotifyCanExecuteChanged(); CancelBatchCommand.NotifyCanExecuteChanged(); }
    public DateOnly Day => DateOnly.FromDateTime((SelectedDate ?? DateTimeOffset.Now).DateTime);
    public ReplenishmentViewModel(ReplenishmentStore store, Func<Task>? validateLabelTransfer = null,
        Func<ReplenishmentLabelBatch, DateOnly, Task>? transferLabels = null, Func<Guid, bool>? batchQueued = null, Func<Task>? showPriceTags = null)
    {
        _store = store; _validateLabelTransfer = validateLabelTransfer; _transferLabels = transferLabels;
        _batchQueued = batchQueued; _showPriceTags = showPriceTags; RefreshSafely();
    }
    partial void OnSelectedDateChanged(DateTimeOffset? value) { if (!IsBusy) RefreshSafely(); }
    partial void OnSearchChanged(string value) => ApplyFilter();
    partial void OnPendingOnlyChanged(bool value) => ApplyFilter();
    partial void OnSelectedRowChanged(ReplenishmentRow? value)
    {
        Quantity = value?.Remaining ?? 0;
        CompleteCommand.NotifyCanExecuteChanged(); CompleteRemainingCommand.NotifyCanExecuteChanged();
    }
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        ConfirmPrintedCommand.NotifyCanExecuteChanged(); CancelBatchCommand.NotifyCanExecuteChanged();
        CompleteCommand.NotifyCanExecuteChanged(); CompleteRemainingCommand.NotifyCanExecuteChanged();
    }
    private void RefreshSafely()
    {
        try { Refresh(); }
        catch (Exception ex) { Message = "Не удалось прочитать учёт довесов: " + ex.Message; IsError = true; }
    }
    private void Refresh()
    {
        var day = _store.Load(Day); _allRows = _store.Rows(Day);
        var selectedLabels = LabelChoices.Where(c => c.IsSelected).ToDictionary(c => c.Key, c => c.QuantityToPrint);
        Replace(LabelChoices, _allRows.Where(r => r.Completed > 0 || r.Printed > 0 || r.Reserved > 0).Select(r =>
        {
            var choice = new ReplenishmentLabelChoice(r);
            if (choice.CanChoose && selectedLabels.TryGetValue(r.Key, out var quantity))
            { choice.IsSelected = true; choice.QuantityToPrint = Math.Min(quantity, choice.Available); }
            return choice;
        }));
        var selectedBatchId = SelectedBatch?.Id;
        Replace(LabelBatches, day.LabelBatches.OrderByDescending(b => b.Created));
        SelectedBatch = LabelBatches.FirstOrDefault(b => b.Id == selectedBatchId);
        LabelSummary = $"Довешено: {day.Events.Sum(e => e.Quantity):0.##} · печать подтверждена: {_allRows.Sum(r => r.Printed):0.##} · в неподтверждённых партиях: {_allRows.Sum(r => r.Reserved):0.##} · доступно ценников: {_allRows.Sum(r => r.LabelsAvailable):0.##}";
        Replace(Events, day.Events.OrderByDescending(e => e.Time));
        Replace(Reports, day.Reports.OrderByDescending(r => r.Time));
        Replace(Exclusions, _store.Rules());
        Summary = $"За {Day:dd.MM.yyyy}: расход {_allRows.Sum(r => Math.Max(0, r.Expense)):0.##} · довешено {day.Events.Sum(e => e.Quantity):0.##} · осталось {_allRows.Sum(r => r.Remaining):0.##} · исключено {_allRows.Where(r => r.Exclusion.Length > 0).Sum(r => Math.Max(0, r.Expense)):0.##} · отчётов {day.Reports.Count}";
        ApplyFilter();
    }
    private void ApplyFilter()
    {
        var key = SelectedRow?.Key;
        var tokens = InventoryKeys.Normalize(Search).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Replace(Rows, _allRows.Where(r => (!PendingOnly || r.Remaining > 0)
            && tokens.All(t => InventoryKeys.Normalize(r.Article + " " + r.Item.Description).Contains(t))));
        SelectedRow = Rows.FirstOrDefault(r => r.Key == key);
    }
    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    { var snapshot = items.ToArray(); collection.Clear(); foreach (var item in snapshot) collection.Add(item); }
    private Task RunAsync(Func<string> action, Action? after = null) => RunOperationAsync(() => Task.Run(action), after);
    private async Task RunOperationAsync(Func<Task<string>> action, Action? after = null)
    {
        if (IsBusy || _stopping) return;
        IsBusy = true; IsError = false;
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); _operation = finished;
        try { Message = await action(); after?.Invoke(); Refresh(); }
        catch (Exception ex) { Message = ex.Message; IsError = true; }
        finally { IsBusy = false; finished.TrySetResult(); _operation = null; }
    }
    private static Window? Owner => (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    [RelayCommand]
    private async Task Import()
    {
        if (IsBusy || Owner is not { } owner) return;
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Ведомость по товарам за день", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath(); if (path is null) return;
        await ImportFileAsync(path);
    }
    public async Task ImportFileAsync(string path)
    {
        InventoryReport? report = null;
        await RunAsync(() => { report = _loader.Load(path); return _store.Import(report); },
            () => SelectedDate = new DateTimeOffset(report!.Day.ToDateTime(TimeOnly.MinValue)));
    }
    [RelayCommand(CanExecute = nameof(CanComplete))]
    private Task Complete() => CompleteQuantity(Quantity);
    [RelayCommand(CanExecute = nameof(CanComplete))]
    private Task CompleteRemaining() => CompleteQuantity(SelectedRow?.Remaining ?? 0);
    private Task CompleteQuantity(decimal quantity)
    {
        if (SelectedRow is not { } row) return Task.CompletedTask;
        var day = Day;
        return RunAsync(() => { _store.Complete(day, row.Key, quantity); return $"Довешено {quantity:0.##}: {row.Article}, {row.Characteristic}. Отметка сохранена."; });
    }
    [RelayCommand]
    private Task Undo()
    {
        if (SelectedEvent is not { } entry) return Task.CompletedTask;
        var day = Day;
        return RunAsync(() => { _store.Undo(day, entry.Id); return "Отметка отменена. История изменения сохранена."; });
    }
    [RelayCommand]
    private Task AddExclusion()
    {
        var kind = RuleType == 0 ? ExclusionKind.Article : ExclusionKind.NameContains; var value = RuleValue;
        return RunAsync(() => { _store.AddRule(kind, value); return "Исключение сохранено и применяется ко всем датам."; }, () => RuleValue = "");
    }
    [RelayCommand]
    private Task ExcludeSelected()
    {
        if (SelectedRow is not { } row) return Task.CompletedTask;
        return RunAsync(() => { _store.AddRule(ExclusionKind.Article, row.Article); return "Артикул добавлен в постоянные исключения (все размеры)."; });
    }
    [RelayCommand]
    private Task RemoveExclusion()
    {
        if (SelectedExclusion is not { } rule) return Task.CompletedTask;
        return RunAsync(() => { _store.RemoveRule(rule.Id); return "Исключение удалено."; });
    }
    [RelayCommand]
    private void SelectLabels()
    {
        foreach (var choice in LabelChoices) choice.IsSelected = choice.CanChoose;
    }
    [RelayCommand]
    private async Task SendLabels()
    {
        if (IsBusy) return;
        var day = Day;
        var selected = LabelChoices.Where(c => c.IsSelected).ToArray();
        ReplenishmentLabelBatch? batch = null;
        var sent = false;
        await RunOperationAsync(async () =>
        {
            if (_validateLabelTransfer is null || _transferLabels is null) throw new InvalidOperationException("Передача в ценники недоступна.");
            if (selected.Length == 0) throw new InvalidOperationException("Отметьте товары для печати.");
            if (selected.Any(c => c.QuantityToPrint <= 0 || c.QuantityToPrint != decimal.Truncate(c.QuantityToPrint) || c.QuantityToPrint > int.MaxValue))
                throw new InvalidOperationException("Количество ценников должно быть положительным целым числом.");
            await _validateLabelTransfer();
            batch = await Task.Run(() => _store.ReserveLabels(day, selected.ToDictionary(c => c.Key, c => (int)c.QuantityToPrint)));
            try
            {
                await _transferLabels(batch, day);
                await Task.Run(() => _store.MarkLabelsSent(day, batch.Id));
            }
            catch (Exception ex)
            {
                RefreshSafely();
                throw new InvalidOperationException($"Партия {batch.Number} сохранена, но передача не подтверждена: {ex.Message} Проверьте очередь ценников. Количество зарезервировано; перед повторной попыткой отмените партию после проверки 1С.", ex);
            }
            sent = true;
            return $"Партия {batch.Number}: {batch.Quantity} ценников передано в очередь. После печати в 1С подтвердите её здесь.";
        });
        if (batch is not null) SelectedBatch = LabelBatches.FirstOrDefault(b => b.Id == batch.Id);
        if (sent && !_stopping && _showPriceTags is not null) await _showPriceTags();
    }
    private static async Task<bool> Confirm(string title, string message)
    {
        if (Owner is not { } owner) return false;
        return await new FanShop.Windows.AgreeDialog { DataContext = new AgreeDialogViewModel { Title = title, Message = message } }.ShowDialog<bool>(owner);
    }
    [RelayCommand(CanExecute = nameof(CanChangeBatch))]
    private async Task ConfirmPrinted()
    {
        if (IsBusy || SelectedBatch is not { } batch) return;
        var day = Day;
        if (!await Confirm("Подтвердить печать?", $"Партия {batch.Number}, {batch.Quantity} ценников. Подтверждайте только после фактической печати в 1С. Количество будет исключено из новых партий за этот день.")) return;
        await RunAsync(() => { _store.ConfirmLabelsPrinted(day, batch.Id); return "Печать подтверждена. Ценники учтены за выбранный день."; });
    }
    [RelayCommand(CanExecute = nameof(CanChangeBatch))]
    private async Task CancelBatch()
    {
        if (IsBusy || SelectedBatch is not { } batch) return;
        var day = Day;
        if (!await Confirm("Отменить партию?", $"Партия {batch.Number}. Сначала остановите и закройте её очередь ценников, удалите переданные строки из 1С, если печать не выполнялась. После отмены количество снова станет доступно для передачи.")) return;
        await RunAsync(() =>
        {
            if (_batchQueued?.Invoke(batch.Id) == true) throw new InvalidOperationException("Эта партия ещё находится в очереди ценников. Сначала остановите и закройте очередь.");
            _store.CancelLabelBatch(day, batch.Id); return "Партия отменена. Количество снова доступно для выбора.";
        });
    }
    [RelayCommand]
    private async Task Export()
    {
        if (IsBusy || Owner is not { } owner) return;
        var day = Day;
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Итог довесов за день", SuggestedFileName = $"Довесы-{day:yyyy-MM-dd}.xlsx", DefaultExtension = "xlsx",
            FileTypeChoices = [new FilePickerFileType("Excel") { Patterns = ["*.xlsx"] }]
        });
        var path = file?.TryGetLocalPath(); if (path is null) return;
        await RunAsync(() => { ReplenishmentExport.Save(path, _store.Load(day), _store.Rows(day)); return "Итог за день сохранён: " + path; });
    }
    public async Task ShutdownAsync()
    {
        _stopping = true;
        if (_operation is { } operation) await operation.Task;
        _store.Dispose();
    }
}

public partial class ReplenishmentLabelChoice : ObservableObject
{
    public ReplenishmentRow Row { get; }
    public string Key => Row.Key;
    public string Article => Row.Article;
    public string Name => Row.Name;
    public string Characteristic => Row.Characteristic;
    public decimal Completed => Row.Completed;
    public decimal Printed => Row.Printed;
    public decimal Reserved => Row.Reserved;
    public decimal Available => decimal.Truncate(Row.LabelsAvailable);
    public bool CanChoose => Available > 0;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private decimal _quantityToPrint;
    public ReplenishmentLabelChoice(ReplenishmentRow row) { Row = row; QuantityToPrint = Available; }
}
