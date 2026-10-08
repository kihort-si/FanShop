namespace FanShop.PriceTags.Models;

public sealed record ProductInfo(string Barcode, string Article, string Name, string Characteristic);
public enum ScanSource { Network, File }
public sealed record ScanRecord(Guid Id, string Barcode, DateTimeOffset Time, ScanSource Source, ProductInfo? Product)
{
    public bool Found => Product is not null;
    public string Result => Found ? "Найден" : "Неизвестный штрихкод";
    public string SourceName => Source == ScanSource.Network ? "Сеть" : "TXT";
}
public sealed record AggregatedProduct(string Article, string Name, string Characteristic, int Quantity);
public enum TransferStatus { Pending, Sending, Completed, Interrupted, Failed }
public sealed record TransferItem(Guid Id, string Article, string Name, string Characteristic, int Quantity, TransferStatus Status = TransferStatus.Pending)
{
    public string StatusName => Status switch { TransferStatus.Pending => "Ожидает", TransferStatus.Sending => "Вводится", TransferStatus.Completed => "Передано", TransferStatus.Interrupted => "Проверить строку 1С", _ => "Ошибка — проверить строку 1С" };
}
public sealed record TransferDelays(int ArticleText = 600, int ArticleEnter = 400, int CharacteristicText = 500, int CharacteristicEnter = 350, int QuantityEnter = 400)
{
    public void Validate()
    {
        if (new[] { ArticleText, ArticleEnter, CharacteristicText, CharacteristicEnter, QuantityEnter }.Any(v => v < 100 || v > 10000))
            throw new InvalidOperationException("Задержки должны быть от 100 до 10000 мс.");
    }
}
public sealed record PriceTagSettings(string? ExcelPath = null, int Port = 8765, TransferDelays? Delays = null);
public sealed record CatalogLoadResult(IReadOnlyList<ProductInfo> Products, IReadOnlyList<string> Warnings);
public sealed record NetworkAddress(string Interface, string Url);
