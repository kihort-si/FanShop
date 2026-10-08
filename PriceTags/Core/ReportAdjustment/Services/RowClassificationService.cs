using System.Text.RegularExpressions;
using FanShop.ReportAdjustment.Models;

namespace FanShop.ReportAdjustment.Services;

public sealed class RowClassificationService
{
    public static string Normalize(string text) => Regex.Replace(text.Trim(), @"\s+", " ").ToUpperInvariant();
    public SalesReportRow Classify(SalesReportRow row, AdjustmentSettings settings)
    {
        var misc = Normalize(row.Characteristic) == "MISC";
        var service = settings.ServiceNames.Any(n => Normalize(n) == Normalize(row.ProductName));
        var size = !misc && !service && Regex.IsMatch(Normalize(row.Characteristic),
            @"^(?:[2-6]?X{0,6}[SML]|ONE SIZE|ONESIZE|OS|\d{1,3}(?:\s*[-–/]\s*\d{1,3})?(?:\s*(?:CM|СМ))?)$", RegexOptions.IgnoreCase);
        return row with { IsMisc = misc, IsService = service, IsSizedProduct = size, UseInSearch = !size };
    }
}
