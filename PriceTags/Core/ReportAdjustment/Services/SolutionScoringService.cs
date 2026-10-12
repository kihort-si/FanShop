using FanShop.ReportAdjustment.Models;

namespace FanShop.ReportAdjustment.Services;

public sealed class SolutionScoringService
{
    public SolutionScore Score(SalesReportRow row, DiscountOption option, AdjustmentSettings settings)
    {
        var rank = Array.IndexOf(settings.DiscountPriorities, option.DiscountPercent);
        if (rank < 0) rank = settings.DiscountPriorities.Length;
        return new(option.Action == AdjustmentAction.Exclude ? 1 : 0, 1, row.IsSizedProduct ? 1 : 0,
            row.PriorityClass == RowPriorityClass.Ordinary ? 1 : 0, row.IsService ? -1 : 0,
            row.PriorityClass == RowPriorityClass.Misc ? -1 : 0, rank,
            option.Action == AdjustmentAction.Exclude ? 0 : Math.Abs(option.DiscountPercent - row.CurrentManualDiscountPercent));
    }
}
