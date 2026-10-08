using System.Globalization;
using ClosedXML.Excel;
using FanShop.PriceTags.Models;
using Microsoft.Extensions.Logging;

namespace FanShop.PriceTags.Services;

public sealed class ExcelCatalogLoader(ILogger<ExcelCatalogLoader> logger)
{
    private static readonly Dictionary<string, string[]> Headers = new()
    {
        ["barcode"] = ["штрихкод", "штрих код", "barcode", "ean"],
        ["article"] = ["артикул", "article"],
        ["name"] = ["номенклатура", "наименование", "название", "name"],
        ["size"] = ["характеристика", "характеристика номенклатуры", "размер", "characteristic"]
    };
    public Task<CatalogLoadResult> LoadAsync(string path, CancellationToken token = default) => Task.Run(() => Load(path, token), token);
    private CatalogLoadResult Load(string path, CancellationToken token)
    {
        try
        {
            using var book = new XLWorkbook(path);
            var products = new Dictionary<string, ProductInfo>(StringComparer.Ordinal);
            var warnings = new List<string>();
            var foundHeader = false;
            foreach (var sheet in book.Worksheets)
            {
                token.ThrowIfCancellationRequested();
                var used = sheet.RangeUsed();
                if (used is null) continue;
                Dictionary<string, int>? columns = null;
                var headerRow = 0;
                foreach (var row in sheet.RowsUsed().Take(50))
                {
                    var map = new Dictionary<string, int>();
                    foreach (var cell in row.CellsUsed())
                    {
                        var value = cell.GetString().Trim().ToLowerInvariant().Replace("ё", "е");
                        foreach (var pair in Headers)
                            if (pair.Value.Contains(value))
                            {
                                if (!map.TryAdd(pair.Key, cell.Address.ColumnNumber))
                                    throw new InvalidDataException($"Лист «{sheet.Name}»: повторяющийся заголовок «{value}».");
                            }
                    }
                    if (map.Count == 4) { columns = map; headerRow = row.RowNumber(); break; }
                }
                if (columns is null) continue;
                foundHeader = true;
                foreach (var row in sheet.Rows(headerRow + 1, used.LastRow().RowNumber()))
                {
                    token.ThrowIfCancellationRequested();
                    Dictionary<string, string> values;
                    try { values = columns.ToDictionary(p => p.Key, p => ReadCell(row.Cell(p.Value), p.Key == "barcode")); }
                    catch (InvalidDataException ex) { warnings.Add($"Лист «{sheet.Name}», строка {row.RowNumber()}: пропущена. {ex.Message}"); continue; }
                    if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
                    if (values.Values.Any(string.IsNullOrWhiteSpace) || values.Values.Any(v => v.Any(char.IsControl)))
                    {
                        warnings.Add($"Лист «{sheet.Name}», строка {row.RowNumber()}: пропущена — заполните все четыре поля без управляющих символов.");
                        continue;
                    }
                    var product = new ProductInfo(values["barcode"], values["article"], values["name"], values["size"]);
                    if (products.TryGetValue(product.Barcode, out var old) && old != product)
                        throw new InvalidDataException($"Конфликт штрихкода {product.Barcode}: «{old.Article} / {old.Characteristic}» и «{product.Article} / {product.Characteristic}». Исправьте Excel.");
                    products[product.Barcode] = product;
                }
            }
            if (!foundHeader) throw new InvalidDataException("Не найдены заголовки: Штрихкод, Артикул, Номенклатура, Характеристика. Заголовки должны быть в первых 50 непустых строках листа.");
            if (products.Count == 0) throw new InvalidDataException("В Excel нет корректных товарных строк. Проверьте заполнение четырёх столбцов.");
            logger.LogInformation("Загружен Excel {Path}: {Count} штрихкодов, {Warnings} пропущенных строк", path, products.Count, warnings.Count);
            return new(products.Values.ToArray(), warnings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { logger.LogError(ex, "Ошибка Excel {Path}", path); throw; }
    }
    private static string ReadCell(IXLCell cell, bool barcode)
    {
        if (cell.DataType == XLDataType.Error) return "";
        if (barcode && cell.DataType == XLDataType.Number)
        {
            var value = cell.GetDouble();
            if (value < 0 || value != Math.Truncate(value) || value >= 1e15)
                throw new InvalidDataException($"Штрихкод в {cell.Address}: Excel сохранил некорректное или слишком длинное число. Задайте текстовый формат и выгрузите заново.");
            var format = cell.Style.NumberFormat.Format;
            // A zero mask preserves zeros present in the Excel representation; missing zeros cannot be guessed.
            if (format.Length > 0 && format.All(c => c == '0')) return value.ToString(format, CultureInfo.InvariantCulture);
            return value.ToString("0", CultureInfo.InvariantCulture);
        }
        return cell.GetFormattedString(CultureInfo.InvariantCulture).Trim();
    }
}
public sealed class ProductCatalogService
{
    private IReadOnlyDictionary<string, ProductInfo> _products = new Dictionary<string, ProductInfo>();
    public int Count => Volatile.Read(ref _products).Count;
    public ProductInfo? Find(string barcode) => Volatile.Read(ref _products).GetValueOrDefault(barcode.Trim());
    public void Replace(IEnumerable<ProductInfo> products)
    {
        var next = new Dictionary<string, ProductInfo>(StringComparer.Ordinal);
        foreach (var p in products)
        {
            var clean = new ProductInfo(p.Barcode.Trim(), p.Article.Trim(), p.Name.Trim(), p.Characteristic.Trim());
            if (new[] { clean.Barcode, clean.Article, clean.Name, clean.Characteristic }.Any(s => string.IsNullOrEmpty(s) || s.Any(char.IsControl)))
                throw new InvalidDataException("Все поля товара должны быть заполнены без управляющих символов.");
            if (next.TryGetValue(clean.Barcode, out var old) && old != clean) throw new InvalidDataException($"Конфликт штрихкода {clean.Barcode}.");
            next[clean.Barcode] = clean;
        }
        if (next.Count == 0) throw new InvalidDataException("Справочник пуст.");
        Volatile.Write(ref _products, next);
    }
}
