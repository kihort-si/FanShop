namespace FanShop.ReportAdjustment.Models;

public enum RowPriorityClass { Service, Misc, Ordinary, Sized }
public enum DiscountRounding { MathematicalRound, DecimalRound2 }
public enum AdjustmentAction { ChangeDiscount, Exclude }

public sealed record SalesReportRow
{
    public int RowNumber { get; init; }
    public int ExcelRowNumber { get; init; }
    public string ProductName { get; init; } = "";
    public string Characteristic { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal BasePrice { get; init; }
    public decimal CurrentManualDiscountPercent { get; init; }
    public decimal? CurrentManualDiscountAmount { get; init; }
    public decimal CurrentTotal { get; init; }
    public string? VatRate { get; init; }
    public decimal? VatAmount { get; init; }
    public string? RealizationDocument { get; init; }
    public bool IsMisc { get; init; }
    public bool IsService { get; init; }
    public bool IsSizedProduct { get; init; }
    public bool IsProtected { get; init; }
    public bool CanChangeDiscount { get; init; } = true;
    public bool CanExclude { get; init; }
    public bool UseInSearch { get; init; } = true;
    public RowPriorityClass PriorityClass => IsService ? RowPriorityClass.Service : IsMisc ? RowPriorityClass.Misc
        : IsSizedProduct ? RowPriorityClass.Sized : RowPriorityClass.Ordinary;
}

public sealed record AdjustmentSettings
{
    public decimal[] AllowedDiscounts { get; init; } = [0, 3, 7, 10, 15, 30];
    public decimal[] DiscountPriorities { get; init; } = [3, 7, 15, 10, 30, 0];
    public bool AllowDiscountDecrease { get; init; }
    public string[] ServiceNames { get; init; } = ["Нанесение Имя/фамилия", "Нанесение однозначная цифра", "Нанесение Малый номер"];
    public int MaxSolutions { get; init; } = 10;
    public DiscountRounding Rounding { get; init; }
    public bool ShowNearest { get; init; }
    public string? LastReportDirectory { get; init; }
    public int MaxPartialSums { get; init; } = 20000;
    public int MaxTransitions { get; init; } = 2_000_000;
    public int MaxSearchSeconds { get; init; } = 15;
    public void Validate()
    {
        if (AllowedDiscounts is null || AllowedDiscounts.Length == 0 || AllowedDiscounts.Length > 100
            || AllowedDiscounts.Any(x => x < 0 || x > 100) || AllowedDiscounts.Distinct().Count() != AllowedDiscounts.Length)
            throw new InvalidOperationException("Задайте 1–100 различных скидок от 0 до 100%.");
        if (DiscountPriorities is null || DiscountPriorities.Distinct().Count() != DiscountPriorities.Length
            || DiscountPriorities.Any(x => !AllowedDiscounts.Contains(x)))
            throw new InvalidOperationException("Приоритеты должны содержать различные разрешённые скидки.");
        if (ServiceNames is null || ServiceNames.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Название услуги не может быть пустым.");
        if (MaxSolutions is < 1 or > 100) throw new InvalidOperationException("Количество вариантов: от 1 до 100.");
        if (MaxPartialSums is < 1 or > 100000 || MaxTransitions < 1 || MaxSearchSeconds is < 1 or > 300)
            throw new InvalidOperationException("Некорректные ограничения поиска.");
        if (!Enum.IsDefined(Rounding)) throw new InvalidOperationException("Неизвестное правило округления.");
    }
}

public sealed record CorrectivePosition(string ProductName, string Characteristic, decimal Quantity, decimal BasePrice, decimal DiscountPercent = 0);
public sealed record AdjustmentRequest(decimal RequiredAmount, IReadOnlyList<CorrectivePosition>? Positions = null)
{
    public decimal GetRequiredAmount(Services.DiscountCalculationService calculator, AdjustmentSettings settings)
    {
        if (Positions is { Count: > 0 })
        {
            foreach (var position in Positions)
                if (string.IsNullOrWhiteSpace(position.ProductName) || position.Quantity <= 0 || position.BasePrice < 0
                    || !settings.AllowedDiscounts.Contains(position.DiscountPercent))
                    throw new InvalidOperationException("Проверьте название, количество, цену и разрешённую скидку корректируемых позиций.");
            return Positions.Sum(p => calculator.Calculate(p.Quantity, p.BasePrice, p.DiscountPercent, settings.Rounding));
        }
        if (RequiredAmount < 0) throw new InvalidOperationException("Сумма корректировки не может быть отрицательной.");
        Money.ToMinor(RequiredAmount);
        return RequiredAmount;
    }
}
public sealed record DiscountOption(decimal DiscountPercent, decimal NewTotal, long Delta, AdjustmentAction Action);
public sealed record RowAdjustment(int RowNumber, decimal Quantity, decimal OriginalDiscount, decimal NewDiscount,
    decimal OriginalTotal, decimal NewTotal, AdjustmentAction Action)
{
    public decimal Delta => OriginalTotal - NewTotal;
}

// Lexicographic, additive criteria: no amount of lower-priority improvement offsets a higher-priority cost.
public readonly record struct SolutionScore(int Exclusions, int ChangedRows, int SizedRows, int OrdinaryRows,
    int NegativeServiceRows, int NegativeMiscRows, int DiscountPriorityCost, decimal DiscountChange) : IComparable<SolutionScore>
{
    public int CompareTo(SolutionScore other)
    {
        var c = Exclusions.CompareTo(other.Exclusions); if (c != 0) return c;
        c = ChangedRows.CompareTo(other.ChangedRows); if (c != 0) return c;
        c = SizedRows.CompareTo(other.SizedRows); if (c != 0) return c;
        c = OrdinaryRows.CompareTo(other.OrdinaryRows); if (c != 0) return c;
        c = NegativeServiceRows.CompareTo(other.NegativeServiceRows); if (c != 0) return c;
        c = NegativeMiscRows.CompareTo(other.NegativeMiscRows); if (c != 0) return c;
        c = DiscountPriorityCost.CompareTo(other.DiscountPriorityCost); if (c != 0) return c;
        return DiscountChange.CompareTo(other.DiscountChange);
    }
    public static SolutionScore operator +(SolutionScore a, SolutionScore b) => new(a.Exclusions+b.Exclusions,
        a.ChangedRows+b.ChangedRows, a.SizedRows+b.SizedRows, a.OrdinaryRows+b.OrdinaryRows,
        a.NegativeServiceRows+b.NegativeServiceRows, a.NegativeMiscRows+b.NegativeMiscRows,
        a.DiscountPriorityCost+b.DiscountPriorityCost, a.DiscountChange+b.DiscountChange);
}
public sealed record AdjustmentSolution(IReadOnlyList<RowAdjustment> Changes, SolutionScore Score, decimal RequiredAmount,
    decimal ControlTotalBefore, decimal ControlTotalAfter, bool RoundingMatchesReport);
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors, decimal ControlBefore, decimal ControlAfter);
public sealed record SalesReport(IReadOnlyList<SalesReportRow> Rows, string FilePath, IReadOnlyList<string> Warnings,
    bool RoundingMatches, int CheckedRows, int MismatchedRows)
{
    public decimal ControlTotal => Rows.Sum(r => r.CurrentTotal);
}
public sealed record SearchProgress(int ProcessedRows, int TotalRows, int PartialSums);
public sealed record SearchResult(IReadOnlyList<AdjustmentSolution> Solutions, bool WasLimited, long Transitions,
    TimeSpan Elapsed, decimal? NearestDifference = null)
{
    public string Message => WasLimited ? "Поиск ограничен: оптимальность и отсутствие других решений не доказаны."
        : Solutions.Count > 0 ? $"Найдено точных вариантов: {Solutions.Count}." : "Точная комбинация не найдена.";
}
public static class Money
{
    public static long ToMinor(decimal amount)
    {
        var minor = amount * 100;
        if (minor != decimal.Truncate(minor)) throw new InvalidOperationException("Денежная сумма должна быть задана с точностью до копейки.");
        return checked((long)minor);
    }
    public static decimal FromMinor(long minor) => minor / 100m;
}
