using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FanShop.ReportAdjustment.Models;
using FanShop.ReportAdjustment.Services;
using Microsoft.Extensions.Logging;

namespace FanShop.ViewModels;

public partial class ReportAdjustmentViewModel : ObservableObject
{
    private readonly SalesReportExcelLoader _loader;
    private readonly AdjustmentSearchService _search;
    private readonly DiscountCalculationService _calculator;
    private readonly RowClassificationService _classifier;
    private readonly SolutionValidator _validator;
    private readonly AdjustmentSettingsService _settingsService;
    private readonly ILogger<ReportAdjustmentViewModel> _logger;
    private AdjustmentSettings _savedSettings = new();
    private CancellationTokenSource? _cancellation;
    private TaskCompletionSource? _operationFinished;
    private long _revision;
    private bool _initialized;
    public ObservableCollection<SalesReportRowViewModel> Rows { get; } = [];
    public ObservableCollection<CorrectivePositionViewModel> Positions { get; } = [];
    public ObservableCollection<AdjustmentSolutionViewModel> Solutions { get; } = [];
    public IReadOnlyList<SalesReportRowViewModel> SelectedRows { get; set; } = [];
    public Array RoundingRules { get; } = Enum.GetValues<DiscountRounding>();
    [ObservableProperty] private string _reportPath = "Отчёт не загружен";
    [ObservableProperty] private string _reportSummary = "Загрузите XLSX с отчётом о продажах.";
    [ObservableProperty] private string _status = "Исходный файл используется только для чтения.";
    [ObservableProperty] private string _roundingWarning = "";
    [ObservableProperty] private string _importWarnings = "";
    [ObservableProperty] private string _nearestMessage = "";
    [ObservableProperty] private string _requiredAmount = "180,00";
    [ObservableProperty] private bool _usePositions;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _progressPercent;
    [ObservableProperty] private bool _canExpand;
    [ObservableProperty] private bool _allowMismatchPreview;
    [ObservableProperty] private string _allowedDiscounts = "0; 3; 7; 10; 15; 30";
    [ObservableProperty] private string _discountPriorities = "3; 7; 15; 10; 30; 0";
    [ObservableProperty] private string _serviceNames = "Нанесение Имя/фамилия\nНанесение однозначная цифра\nНанесение Малый номер";
    [ObservableProperty] private bool _allowDiscountDecrease;
    [ObservableProperty] private bool _showNearest;
    [ObservableProperty] private int _maxSolutions = 10;
    [ObservableProperty] private DiscountRounding _rounding;
    [ObservableProperty] private AdjustmentSolutionViewModel? _selectedSolution;
    [ObservableProperty] private CorrectivePositionViewModel? _selectedPosition;
    [ObservableProperty] private bool _showFullReport;
    public bool CanEdit => !IsBusy;
    public bool HasRoundingWarning => RoundingWarning.Length > 0;
    public IEnumerable<AdjustmentPreviewRow> PreviewRows => (ShowFullReport ? SelectedSolution?.FullReport : SelectedSolution?.Changes) ?? [];
    public string SolutionSummary => SelectedSolution?.Summary ?? "Выберите найденный вариант для просмотра изменений.";

    public ReportAdjustmentViewModel(SalesReportExcelLoader loader, AdjustmentSearchService search, DiscountCalculationService calculator,
        RowClassificationService classifier, SolutionValidator validator, AdjustmentSettingsService settings, ILogger<ReportAdjustmentViewModel> logger)
    {
        _loader = loader; _search = search; _calculator = calculator; _classifier = classifier; _validator = validator; _settingsService = settings; _logger = logger;
        try { _savedSettings = settings.Load(); ApplySettings(_savedSettings); }
        catch (Exception ex) { Status = "Настройки не загружены: " + ex.Message; _logger.LogWarning(ex, "Не удалось загрузить настройки корректировки"); }
        _initialized = true;
    }
    private static readonly HashSet<string> Inputs = [nameof(RequiredAmount), nameof(UsePositions), nameof(AllowedDiscounts), nameof(DiscountPriorities),
        nameof(ServiceNames), nameof(AllowDiscountDecrease), nameof(ShowNearest), nameof(MaxSolutions), nameof(Rounding), nameof(AllowMismatchPreview)];
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_initialized && e.PropertyName is { } name && Inputs.Contains(name))
        {
            Invalidate();
            if (name == nameof(Rounding)) CheckRounding();
        }
    }
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));
    partial void OnRoundingWarningChanged(string value) => OnPropertyChanged(nameof(HasRoundingWarning));
    partial void OnSelectedSolutionChanged(AdjustmentSolutionViewModel? value) { OnPropertyChanged(nameof(PreviewRows)); OnPropertyChanged(nameof(SolutionSummary)); }
    partial void OnShowFullReportChanged(bool value) => OnPropertyChanged(nameof(PreviewRows));
    private void Invalidate()
    {
        if (Solutions.Count > 0 || SelectedSolution is not null || NearestMessage.Length > 0)
            Status = "Параметры изменились. Повторите подбор.";
        _revision++; Solutions.Clear(); SelectedSolution = null; NearestMessage = ""; CanExpand = false;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Invalidate();
    private void ApplySettings(AdjustmentSettings s)
    {
        AllowedDiscounts = string.Join("; ", s.AllowedDiscounts); DiscountPriorities = string.Join("; ", s.DiscountPriorities);
        ServiceNames = string.Join('\n', s.ServiceNames); AllowDiscountDecrease = s.AllowDiscountDecrease;
        MaxSolutions = s.MaxSolutions; Rounding = s.Rounding; ShowNearest = s.ShowNearest;
    }
    private AdjustmentSettings ReadSettings()
    {
        static decimal[] Discounts(string value) => value.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => SalesReportExcelLoader.ParseNumber(x)).ToArray();
        var settings = _savedSettings with { AllowedDiscounts = Discounts(AllowedDiscounts), DiscountPriorities = Discounts(DiscountPriorities),
            ServiceNames = ServiceNames.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            AllowDiscountDecrease = AllowDiscountDecrease, MaxSolutions = MaxSolutions, Rounding = Rounding, ShowNearest = ShowNearest };
        settings.Validate(); return settings;
    }
    private void CheckRounding()
    {
        var mismatches = Rows.Count(r => !_calculator.Matches(r.Original, Rounding));
        RoundingWarning = mismatches == 0 ? "" : DiscountCalculationService.MismatchWarning + $" Несовпадений: {mismatches} из {Rows.Count}.";
    }
    private void ReplaceRows(IEnumerable<SalesReportRow> rows)
    {
        foreach (var old in Rows) old.PropertyChanged -= Changed;
        Rows.Clear(); SelectedRows = [];
        foreach (var row in rows) { var item = new SalesReportRowViewModel(row); item.PropertyChanged += Changed; Rows.Add(item); }
        Invalidate(); CheckRounding();
    }
    private static Window? Owner() => (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    [RelayCommand] private async Task LoadReport()
    {
        if (IsBusy) return;
        try
        {
            var owner = Owner() ?? throw new InvalidOperationException("Окно приложения недоступно.");
            var folder = _savedSettings.LastReportDirectory is { } directory && Directory.Exists(directory)
                ? await owner.StorageProvider.TryGetFolderFromPathAsync(directory) : null;
            var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Отчёт о продажах", AllowMultiple = false,
                SuggestedStartLocation = folder, FileTypeFilter = [new FilePickerFileType("Excel XLSX") { Patterns = ["*.xlsx"] }] });
            if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) await ImportReportAsync(path);
        }
        catch (Exception ex) { Fail(ex); }
    }
    public async Task ImportReportAsync(string path)
    {
        if (IsBusy) return;
        IsBusy = true; ProgressPercent = 0; _cancellation = new(); _operationFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var settings = ReadSettings();
            var operation = Task.Run(() => _loader.Load(path, settings, _cancellation.Token), _cancellation.Token);
            var report = await operation;
            ReplaceRows(report.Rows); ReportPath = Path.GetFullPath(path);
            ReportSummary = $"Строк: {Rows.Count} • Исходная сумма: {AdjustmentSolutionViewModel.Rubles(report.ControlTotal)}";
            ImportWarnings = string.Join('\n', report.Warnings.Where(w => !w.StartsWith(DiscountCalculationService.MismatchWarning)));
            _savedSettings = settings with { LastReportDirectory = Path.GetDirectoryName(ReportPath) };
            _settingsService.Save(_savedSettings); Status = "Отчёт загружен. Выберите допустимые строки и задайте корректировку.";
            _logger.LogInformation("Импортировано строк: {Count}; проверено правилом расчёта: {Checked}; несовпадений: {Mismatch}", Rows.Count, report.CheckedRows, report.MismatchedRows);
        }
        catch (OperationCanceledException) { Status = "Импорт отменён."; }
        catch (Exception ex) { Fail(ex); }
        finally { EndOperation(); }
    }
    [RelayCommand] private async Task FindSolutions() => await FindSolutionsAsync();
    public async Task FindSolutionsAsync()
    {
        if (IsBusy) return;
        Invalidate();
        try
        {
            if (Rows.Count == 0) throw new InvalidOperationException("Сначала загрузите отчёт.");
            var settings = ReadSettings();
            // Reclassification updates priorities while keeping all explicit row permissions.
            var rows = Rows.Select(r => { var snapshot = r.Snapshot(); return _classifier.Classify(snapshot, settings) with { UseInSearch = snapshot.UseInSearch }; }).ToArray();
            CheckRounding();
            if (HasRoundingWarning && !AllowMismatchPreview)
                throw new InvalidOperationException("Выберите соответствующее правило округления либо явно разрешите расчётный предпросмотр без гарантии соответствия 1С.");
            if (UsePositions && Positions.Count == 0) throw new InvalidOperationException("Добавьте хотя бы одну корректируемую позицию.");
            var request = new AdjustmentRequest(UsePositions ? 0 : SalesReportExcelLoader.ParseNumber(RequiredAmount),
                UsePositions ? Positions.Select(p => p.Snapshot()).ToArray() : null);
            request.GetRequiredAmount(_calculator, settings);
            _settingsService.Save(settings); _savedSettings = settings;
            IsBusy = true; ProgressPercent = 0; _cancellation = new(); var revision = _revision;
            _operationFinished = new(TaskCreationOptions.RunContinuationsAsynchronously); var searchCancellation = _cancellation;
            Status = "Подбор точных вариантов…";
            var progress = new Progress<SearchProgress>(p => { if (IsBusy && ReferenceEquals(searchCancellation, _cancellation)) { ProgressPercent = p.TotalRows == 0 ? 100 : p.ProcessedRows * 100 / p.TotalRows;
                Status = $"Обработано строк: {p.ProcessedRows}/{p.TotalRows}; промежуточных сумм: {p.PartialSums}."; } });
            var operation = _search.SearchAsync(rows, request, settings, progress, _cancellation.Token);
            var result = await operation;
            if (revision != _revision) { Status = "Параметры изменились во время поиска. Повторите подбор."; return; }
            foreach (var solution in result.Solutions)
            {
                var validation = _validator.Validate(rows, request, settings, solution);
                if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors));
                Solutions.Add(new(solution, rows, request, Solutions.Count + 1, !result.WasLimited));
            }
            SelectedSolution = Solutions.FirstOrDefault(); ProgressPercent = 100;
            Status = result.Message + $" Проверено переходов: {result.Transitions:N0}; время: {result.Elapsed.TotalSeconds:F2} с.";
            if (result.Solutions.Count == 0 && !result.WasLimited)
            { CanExpand = Rows.Any(r => r.Original.IsSizedProduct && !r.IsProtected && !r.UseInSearch); Status += " Среди предпочтительных строк точное решение не найдено."; }
            if (result.NearestDifference is { } difference) NearestMessage = $"НЕ ЯВЛЯЕТСЯ ТОЧНЫМ РЕШЕНИЕМ. Ближайшее найденное отклонение: {AdjustmentSolutionViewModel.Rubles(difference)}";
            if (HasRoundingWarning) Status += " Результаты — только расчётный предпросмотр; соответствие 1С не подтверждено.";
            _logger.LogInformation("Подбор: {Solutions} решений; {Transitions} переходов; ограничен={Limited}", Solutions.Count, result.Transitions, result.WasLimited);
        }
        catch (OperationCanceledException) { Status = "Поиск отменён. Неполные результаты не показаны."; }
        catch (Exception ex) { Fail(ex); }
        finally { EndOperation(); }
    }
    [RelayCommand] private void CancelSearch() => _cancellation?.Cancel();
    private void EndOperation()
    {
        _cancellation?.Dispose(); _cancellation = null; IsBusy = false;
        _operationFinished?.TrySetResult(); _operationFinished = null;
    }
    [RelayCommand] private void ProtectSelected() { if (!IsBusy) foreach (var row in SelectedRows) row.IsProtected = true; }
    [RelayCommand] private void UnprotectSelected() { if (!IsBusy) foreach (var row in SelectedRows) row.IsProtected = false; }
    [RelayCommand] private void ProtectSized() { if (!IsBusy) foreach (var row in Rows.Where(r => r.Original.IsSizedProduct)) row.IsProtected = true; }
    [RelayCommand] private async Task ExpandSearch()
    {
        if (IsBusy || !CanExpand) return;
        foreach (var row in Rows.Where(r => r.Original.IsSizedProduct && !r.IsProtected)) row.UseInSearch = true;
        await FindSolutionsAsync();
    }
    [RelayCommand] private void AddPosition()
    { if (IsBusy) return; var item = new CorrectivePositionViewModel(); item.PropertyChanged += Changed; Positions.Add(item); SelectedPosition = item; Invalidate(); }
    [RelayCommand] private void RemovePosition()
    { if (IsBusy || SelectedPosition is null) return; SelectedPosition.PropertyChanged -= Changed; Positions.Remove(SelectedPosition); SelectedPosition = null; Invalidate(); }
    [RelayCommand] private void SaveSettings()
    {
        if (IsBusy) return;
        try { var settings = ReadSettings(); _settingsService.Save(settings); _savedSettings = settings;
            ReplaceRows(Rows.Select(r => { var snapshot = r.Snapshot(); return _classifier.Classify(snapshot, settings) with { UseInSearch = snapshot.UseInSearch }; }).ToArray());
            Status = "Настройки сохранены. Список услуг и приоритеты обновлены."; }
        catch (Exception ex) { Fail(ex); }
    }
    [RelayCommand] private async Task CopyInstruction()
    {
        try { if (SelectedSolution is null) throw new InvalidOperationException("Сначала выберите решение.");
            var clipboard = Owner()?.Clipboard ?? throw new InvalidOperationException("Буфер обмена недоступен.");
            await clipboard.SetTextAsync(SelectedSolution.Instruction); Status = "Инструкция скопирована для ручной проверки и оформления."; }
        catch (Exception ex) { Fail(ex); }
    }
    private void Fail(Exception ex) { Status = ex.Message; _logger.LogError(ex, "Ошибка корректировки отчёта"); }
    public async Task ShutdownAsync()
    { _cancellation?.Cancel(); if (_operationFinished is { } completion) await completion.Task; }
}
