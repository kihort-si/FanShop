using FanShop.ReportAdjustment.Models;

namespace FanShop.ReportAdjustment.Services;

public sealed class SolutionValidator(DiscountCalculationService calculator)
{
    public ValidationResult Validate(IReadOnlyList<SalesReportRow> rows, AdjustmentRequest request,
        AdjustmentSettings settings, AdjustmentSolution solution)
    {
        var errors = new List<string>();
        decimal before = rows.Sum(r => r.CurrentTotal), after = before;
        try
        {
            settings.Validate();
            var required = request.GetRequiredAmount(calculator, settings);
            var seen = new HashSet<int>();
            if (rows.Select(r => r.RowNumber).Distinct().Count() != rows.Count) errors.Add("Неуникальные номера строк отчёта.");
            foreach (var change in solution.Changes)
            {
                if (!seen.Add(change.RowNumber)) { errors.Add("Строка изменена дважды."); continue; }
                var row = rows.FirstOrDefault(r => r.RowNumber == change.RowNumber);
                if (row is null) { errors.Add($"Строка {change.RowNumber} не существует."); continue; }
                if (row.IsProtected || !row.UseInSearch) errors.Add($"Строка {row.RowNumber} защищена или не разрешена для подбора.");
                if (change.Quantity != row.Quantity || change.OriginalTotal != row.CurrentTotal || change.OriginalDiscount != row.CurrentManualDiscountPercent)
                    errors.Add($"Исходные данные строки {row.RowNumber} изменились.");
                if (change.Action == AdjustmentAction.Exclude)
                {
                    if (!row.CanExclude || change.NewTotal != 0 || change.NewDiscount != row.CurrentManualDiscountPercent)
                        errors.Add($"Исключение строки {row.RowNumber} не разрешено или неверно описано.");
                }
                else if (change.Action == AdjustmentAction.ChangeDiscount)
                {
                    if (!row.CanChangeDiscount || !settings.AllowedDiscounts.Contains(change.NewDiscount)
                        || change.NewDiscount == row.CurrentManualDiscountPercent
                        || (!settings.AllowDiscountDecrease && change.NewDiscount < row.CurrentManualDiscountPercent))
                        errors.Add($"Скидка строки {row.RowNumber} не разрешена.");
                    var calculated = calculator.Calculate(row.Quantity, row.BasePrice, change.NewDiscount, settings.Rounding);
                    if (calculated != change.NewTotal) errors.Add($"Сумма строки {row.RowNumber} рассчитана неверно.");
                }
                else errors.Add("Неизвестное действие.");
                Money.ToMinor(change.NewTotal);
                after += change.NewTotal - row.CurrentTotal;
            }
            after += required; // Explicit corrective positions or the separately authorized amount, never a fabricated product.
            if (solution.Changes.Sum(c => c.Delta) != required) errors.Add("Компенсация не равна требуемой сумме.");
            if (before != after || solution.ControlTotalBefore != before || solution.ControlTotalAfter != after || solution.RequiredAmount != required)
                errors.Add("Контрольная сумма не сохраняется.");
            var matches = rows.All(r => calculator.Matches(r, settings.Rounding));
            if (solution.RoundingMatchesReport != matches) errors.Add("Неверный признак соответствия правила округления.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or OverflowException) { errors.Add(ex.Message); }
        return new(errors.Count == 0, errors, before, after);
    }
}
