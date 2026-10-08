using FanShop.ReportAdjustment.Models;

namespace FanShop.ReportAdjustment.Services;

public interface IDiscountRoundingStrategy
{
    decimal Calculate(decimal quantity, decimal basePrice, decimal discountPercent);
}
public sealed class MathematicalRoundStrategy : IDiscountRoundingStrategy
{
    public decimal Calculate(decimal quantity, decimal basePrice, decimal discountPercent)
    {
        var gross = quantity * basePrice;
        var roundedDiscount = Math.Min(gross, decimal.Round(gross * discountPercent / 100m, 0, MidpointRounding.AwayFromZero));
        return decimal.Round(gross - roundedDiscount, 2, MidpointRounding.AwayFromZero);
    }
}
public sealed class DecimalRound2Strategy : IDiscountRoundingStrategy
{
    public decimal Calculate(decimal quantity, decimal basePrice, decimal discountPercent)
    {
        var gross = quantity * basePrice;
        return decimal.Round(gross - decimal.Round(gross * discountPercent / 100m, 2, MidpointRounding.AwayFromZero), 2, MidpointRounding.AwayFromZero);
    }
}
public sealed class DiscountCalculationService
{
    public const string MismatchWarning = "Правило расчёта скидки не соответствует данным отчёта. Результаты подбора могут быть некорректны.";
    private readonly IReadOnlyDictionary<DiscountRounding, IDiscountRoundingStrategy> _strategies;
    public DiscountCalculationService() : this(new Dictionary<DiscountRounding, IDiscountRoundingStrategy>
    { [DiscountRounding.MathematicalRound] = new MathematicalRoundStrategy(), [DiscountRounding.DecimalRound2] = new DecimalRound2Strategy() }) { }
    public DiscountCalculationService(IReadOnlyDictionary<DiscountRounding, IDiscountRoundingStrategy> strategies) => _strategies = strategies;
    public decimal Calculate(decimal quantity, decimal price, decimal discount, DiscountRounding rounding)
    {
        if (quantity <= 0 || price < 0 || discount < 0 || discount > 100) throw new InvalidOperationException("Некорректные параметры расчёта скидки.");
        var total = _strategies[rounding].Calculate(quantity, price, discount);
        Money.ToMinor(total);
        return total;
    }
    public bool Matches(SalesReportRow row, DiscountRounding rounding) => Calculate(row.Quantity, row.BasePrice, row.CurrentManualDiscountPercent, rounding) == row.CurrentTotal;
}
