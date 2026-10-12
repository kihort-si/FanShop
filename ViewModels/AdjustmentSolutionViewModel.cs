using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using FanShop.ReportAdjustment.Models;

namespace FanShop.ViewModels;

public partial class SalesReportRowViewModel : ObservableObject
{
    public SalesReportRow Original { get; }
    [ObservableProperty] private bool _isProtected;
    [ObservableProperty] private bool _useInSearch;
    [ObservableProperty] private bool _canExclude;
    [ObservableProperty] private bool _canChangeDiscount;
    public SalesReportRowViewModel(SalesReportRow row)
    { Original = row; _isProtected = row.IsProtected; _useInSearch = row.UseInSearch; _canExclude = row.CanExclude; _canChangeDiscount = row.CanChangeDiscount; }
    public SalesReportRow Snapshot() => Original with { IsProtected = IsProtected, UseInSearch = UseInSearch, CanExclude = CanExclude, CanChangeDiscount = CanChangeDiscount };
    public int RowNumber => Original.RowNumber;
    public string ProductName => Original.ProductName;
    public string Characteristic => Original.Characteristic;
    public decimal Quantity => Original.Quantity;
    public decimal BasePrice => Original.BasePrice;
    public decimal CurrentDiscount => Original.CurrentManualDiscountPercent;
    public decimal CurrentTotal => Original.CurrentTotal;
    public string Category => Original.PriorityClass switch { RowPriorityClass.Service => "Услуга", RowPriorityClass.Misc => "MISC", RowPriorityClass.Sized => "Размерный", _ => "Обычный" };
    public string CategoryColor => Original.PriorityClass switch { RowPriorityClass.Service => "#246B56", RowPriorityClass.Misc => "#275D91", RowPriorityClass.Sized => "#795F36", _ => "#64748B" };
    public string State => IsProtected ? "Защищена" : UseInSearch ? "В подборе" : "Не участвует";
    partial void OnIsProtectedChanged(bool value) => OnPropertyChanged(nameof(State));
    partial void OnUseInSearchChanged(bool value) => OnPropertyChanged(nameof(State));
}

public partial class CorrectivePositionViewModel : ObservableObject
{
    [ObservableProperty] private string _productName = "";
    [ObservableProperty] private string _characteristic = "MISC";
    [ObservableProperty] private string _quantity = "1";
    [ObservableProperty] private string _basePrice = "0";
    [ObservableProperty] private string _discount = "0";
    public CorrectivePosition Snapshot() => new(ProductName, Characteristic,
        FanShop.ReportAdjustment.Services.SalesReportExcelLoader.ParseNumber(Quantity),
        FanShop.ReportAdjustment.Services.SalesReportExcelLoader.ParseNumber(BasePrice),
        FanShop.ReportAdjustment.Services.SalesReportExcelLoader.ParseNumber(Discount));
}

public sealed record AdjustmentPreviewRow(int RowNumber, string ProductName, string Characteristic, decimal Quantity,
    decimal OldDiscount, decimal NewDiscount, decimal OldTotal, decimal NewTotal, decimal Delta, string Action,
    string? VatRate = null, decimal? OriginalVatAmount = null, string? RealizationDocument = null);

public sealed class AdjustmentSolutionViewModel
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    public AdjustmentSolution Solution { get; }
    public string Title { get; }
    public IReadOnlyList<AdjustmentPreviewRow> Changes { get; }
    public IReadOnlyList<AdjustmentPreviewRow> FullReport { get; }
    public string Summary { get; }
    public string Instruction { get; }
    public static string Rubles(decimal value) => value.ToString("N2", Russian) + " ₽";
    public AdjustmentSolutionViewModel(AdjustmentSolution solution, IReadOnlyList<SalesReportRow> rows, AdjustmentRequest request, int number, bool searchComplete = true)
    {
        Solution = solution;
        Title = $"Вариант {number} • изменений: {solution.Changes.Count}"
            + (!solution.RoundingMatchesReport ? " • расчётный предпросмотр" : number == 1 ? searchComplete ? " • рекомендуется" : " • лучший из найденных" : "");
        var changes = solution.Changes.ToDictionary(c => c.RowNumber);
        FullReport = rows.Select(row =>
        {
            changes.TryGetValue(row.RowNumber, out var change);
            return new AdjustmentPreviewRow(row.RowNumber, row.ProductName, row.Characteristic, row.Quantity,
                row.CurrentManualDiscountPercent, change?.NewDiscount ?? row.CurrentManualDiscountPercent,
                row.CurrentTotal, change?.NewTotal ?? row.CurrentTotal, change?.Delta ?? 0,
                change?.Action == AdjustmentAction.Exclude ? "ИСКЛЮЧИТЬ СТРОКУ" : change is null ? "Без изменений" : "Изменить скидку",
                row.VatRate, row.VatAmount, row.RealizationDocument);
        }).ToArray();
        Changes = FullReport.Where(r => changes.ContainsKey(r.RowNumber)).ToArray();
        var proof = solution.RoundingMatchesReport ? "Проверено для выбранного правила расчёта." : "РАСЧЁТНЫЙ ПРЕДПРОСМОТР: правило не совпадает с отчётом; точность в 1С не подтверждена.";
        Summary = $"Общая компенсация: {Rubles(solution.Changes.Sum(c => c.Delta))}\n"
            + $"Сумма оставшихся строк: {Rubles(FullReport.Sum(r => r.NewTotal))}\n"
            + $"Корректировка: +{Rubles(solution.RequiredAmount)}\nКонтрольная сумма ДО: {Rubles(solution.ControlTotalBefore)}\n"
            + $"Контрольная сумма ПОСЛЕ: {Rubles(solution.ControlTotalAfter)}\nРазница: {Rubles(solution.ControlTotalAfter - solution.ControlTotalBefore)}\n{proof}";
        var text = new StringBuilder($"Корректировка: {Rubles(solution.RequiredAmount)}\n{proof}\n\n");
        if (request.Positions is { Count: > 0 })
        {
            text.AppendLine("Официально учитываемые позиции:");
            foreach (var p in request.Positions) text.AppendLine($"• {p.ProductName} ({p.Characteristic}), {p.Quantity} × {Rubles(p.BasePrice)}, скидка {p.DiscountPercent}%");
        }
        else text.AppendLine("Отдельно учитываемая разрешённая сумма (без создания фиктивного товара).");
        var index = 0;
        foreach (var row in Changes)
            text.AppendLine($"\n{++index}. Строка {row.RowNumber} — {row.ProductName} ({row.Characteristic})\n   Действие: {row.Action}\n   Количество: {row.Quantity}\n   Скидка: {row.OldDiscount}% → {row.NewDiscount}%\n   Сумма: {Rubles(row.OldTotal)} → {Rubles(row.NewTotal)}\n   Разница: {Rubles(row.Delta)}");
        text.AppendLine("\nКомпенсация: " + (Changes.Count == 0 ? "0" : string.Join(" + ", Changes.Select(c => Rubles(c.Delta)))) + " = " + Rubles(solution.RequiredAmount));
        text.AppendLine(Summary);
        text.AppendLine("НДС и документ реализации сохранены как исходные сведения; НДС после корректировки требуется пересчитать и проверить в 1С.");
        Instruction = text.ToString();
    }
}
