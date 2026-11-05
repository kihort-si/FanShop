using System.Text.RegularExpressions;

namespace FanShop.Replenishment;

public static class InventoryKeys
{
    public static string Normalize(string? value) => Regex.Replace((value ?? string.Empty).Trim(), @"\s+", " ").ToUpperInvariant().Replace('Ё', 'Е');
}
public sealed record InventoryItem(string Warehouse, string Article, string Description,
    decimal Opening, decimal Incoming, decimal Expense, decimal Closing)
{
    public string Key => System.Text.Json.JsonSerializer.Serialize(new[] {
        InventoryKeys.Normalize(Article), InventoryKeys.Normalize(Description) });
    public string Name => Description.Contains(',') ? Description[..Description.LastIndexOf(',')].Trim() : Description;
    public string Characteristic => Description.Contains(',') ? Description[(Description.LastIndexOf(',') + 1)..].Trim() : "";
}
public sealed record InventoryReport(DateOnly Day, string FileName, string Hash, InventoryItem[] Items);
public enum ExclusionKind { Article, NameContains }
public sealed record ExclusionRule(Guid Id, ExclusionKind Kind, string Value)
{
    public string KindName => Kind == ExclusionKind.Article ? "Артикул" : "Слова в названии";
    public bool Matches(InventoryItem item) => Kind == ExclusionKind.Article
        ? InventoryKeys.Normalize(item.Article) == InventoryKeys.Normalize(Value)
        : InventoryKeys.Normalize(item.Description).Contains(InventoryKeys.Normalize(Value), StringComparison.Ordinal);
}
public sealed record ImportedReport(Guid Id, DateTimeOffset Time, string FileName, string Hash,
    int ItemCount, decimal Expense, string Mode, string[] Warehouses);
public sealed record ReplenishmentEvent(Guid Id, DateTimeOffset Time, InventoryItem Item, decimal Quantity, Guid? UndoOf = null)
{
    public string Action => UndoOf is null ? "Довешено" : "Отмена отметки";
}
public sealed class ReplenishmentDay
{
    public int Format { get; set; } = 2;
    public DateOnly Day { get; set; }
    public List<InventoryItem> Items { get; set; } = [];
    public List<ImportedReport> Reports { get; set; } = [];
    public List<ReplenishmentEvent> Events { get; set; } = [];
    public List<ReplenishmentLabelBatch> LabelBatches { get; set; } = [];
}
public sealed record ReplenishmentRow(InventoryItem Item, decimal Completed, string Exclusion, decimal Printed = 0, decimal Reserved = 0)
{
    public string Key => Item.Key;
    public string Article => Item.Article;
    public string Name => Item.Name;
    public string Characteristic => Item.Characteristic;
    public decimal Opening => Item.Opening;
    public decimal Incoming => Item.Incoming;
    public decimal Expense => Item.Expense;
    public decimal Closing => Item.Closing;
    public decimal Remaining => Exclusion.Length > 0 || Expense < 1 ? 0 : Math.Max(0, Expense - Completed);
    public decimal LabelsAvailable => Math.Max(0, Completed - Printed - Reserved);
    public string Status => Exclusion.Length > 0 ? "Исключение" : Remaining > 0 ? Completed > 0 ? "Частично довешено" : "Нужно довесить" : "Довешено";
}

public sealed record ReplenishmentLabelLine(Guid TransferId, string Key, string Article, string Name, string Characteristic, int Quantity);
public sealed record ReplenishmentLabelBatch(Guid Id, DateTimeOffset Created, ReplenishmentLabelLine[] Lines,
    DateTimeOffset? Sent = null, DateTimeOffset? Printed = null, DateTimeOffset? Cancelled = null)
{
    public string Number => Id.ToString("N")[..8];
    public int Quantity => Lines.Sum(l => l.Quantity);
    public string PrintedText => Printed?.ToString("dd.MM.yyyy HH:mm:ss") ?? "Не подтверждена";
    public string Status => Cancelled is not null ? "Отменена" : Printed is not null ? "Печать подтверждена" :
        Sent is not null ? "Передано в раздел ценников; подтвердите печать" : "Передача не подтверждена — проверьте очередь";
}
public static class InventoryAggregation
{
    public static InventoryItem[] Merge(IEnumerable<InventoryItem> items) => items.GroupBy(i => i.Key)
        .Select(g => g.First() with { Warehouse = "", Opening = g.Sum(i => i.Opening), Incoming = g.Sum(i => i.Incoming),
            Expense = g.Sum(i => i.Expense), Closing = g.Sum(i => i.Closing) }).ToArray();
}
