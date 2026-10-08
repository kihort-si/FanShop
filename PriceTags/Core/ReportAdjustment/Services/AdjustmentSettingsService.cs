using FanShop.PriceTags.Services;
using FanShop.ReportAdjustment.Models;

namespace FanShop.ReportAdjustment.Services;

public sealed class AdjustmentSettingsService(LocalStorage storage)
{
    public AdjustmentSettings Load()
    {
        var settings = storage.Read<AdjustmentSettings>("adjustment-settings.json") ?? new();
        settings.Validate();
        return settings;
    }
    public void Save(AdjustmentSettings settings) { settings.Validate(); storage.Write("adjustment-settings.json", settings); }
}
