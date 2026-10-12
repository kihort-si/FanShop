using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using FanShop.ReportAdjustment.Models;

namespace FanShop.ReportAdjustment.Services;

public sealed class SalesReportExcelLoader(RowClassificationService classifier, DiscountCalculationService calculator)
{
    private static readonly Dictionary<string, string[]> Aliases = new()
    {
        ["number"] = ["n", "№", "номер", "номер строки", "п/п", "n п/п"],
        ["name"] = ["номенклатура", "товар", "наименование", "наименование номенклатуры"],
        ["characteristic"] = ["характеристика", "характеристика номенклатуры", "размер"],
        ["quantity"] = ["продано", "количество", "кол-во", "количество продано"],
        ["price"] = ["цена", "базовая цена", "цена до скидки"],
        ["discount"] = ["% руч", "% ручной скидки", "ручная скидка %", "скидка %", "процент ручной скидки"],
        ["total"] = ["сумма", "сумма продажи", "сумма после скидки"],
        ["discountAmount"] = ["сумма руч", "сумма ручной скидки"],
        ["vatRate"] = ["ставка ндс", "ндс %"], ["vatAmount"] = ["сумма ндс"],
        ["document"] = ["документ реализации", "документ продажи"]
    };
    private static readonly string[] Required = ["number", "name", "characteristic", "quantity", "price", "discount", "total"];
    private static string Header(string value) => Regex.Replace(RowClassificationService.Normalize(value).ToLowerInvariant().Replace(".", ""), @"\s+", " ").Trim();
    public static decimal ParseNumber(string text, bool emptyIsZero = false)
    {
        if (string.IsNullOrWhiteSpace(text) && emptyIsZero) return 0;
        var cleaned = Regex.Replace(text, @"[\s₽%]", "").Replace("руб.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("руб", "", StringComparison.OrdinalIgnoreCase).Replace(',', '.');
        if (!decimal.TryParse(cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
            throw new FormatException($"Некорректное число: «{text}».");
        return number;
    }
    private static decimal Number(IXLCell cell, bool percent = false, bool emptyIsZero = false)
    {
        if (cell.DataType == XLDataType.Number)
        {
            var value = cell.GetValue<decimal>();
            // Native Excel percentages are stored as fractions; text "3%" is already in percentage points.
            if (percent && (cell.Style.NumberFormat.Format.Contains('%') || cell.Style.NumberFormat.NumberFormatId is 9 or 10)) value *= 100;
            return value;
        }
        return ParseNumber(cell.GetString(), emptyIsZero);
    }
    public SalesReport Load(string path, AdjustmentSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Validate();
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(input);
        foreach (var sheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var last = sheet.LastRowUsed()?.RowNumber() ?? 0;
            for (var headerRow = 1; headerRow <= Math.Min(last, 100); headerRow++)
            {
                var columns = new Dictionary<string, int>();
                foreach (var cell in sheet.Row(headerRow).CellsUsed())
                {
                    var heading = Header(cell.GetString());
                    foreach (var alias in Aliases.Where(a => a.Value.Any(v => Header(v) == heading)))
                    {
                        if (!columns.TryAdd(alias.Key, cell.Address.ColumnNumber))
                            throw new InvalidDataException($"Повторяющийся столбец «{heading}», лист «{sheet.Name}».");
                    }
                }
                if (!Required.All(columns.ContainsKey)) continue;
                var rows = new List<SalesReportRow>();
                var warnings = new List<string>();
                var ids = new HashSet<int>();
                for (var i = headerRow + 1; i <= last; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    IXLCell Cell(string key) => sheet.Cell(i, columns[key]);
                    string? Text(string key) => columns.ContainsKey(key) ? Cell(key).GetFormattedString().Trim() : null;
                    decimal? Optional(string key) => columns.ContainsKey(key) && !Cell(key).IsEmpty() ? Number(Cell(key)) : null;
                    if (sheet.Row(i).IsEmpty()) continue;
                    var numberText = Cell("number").GetFormattedString().Trim();
                    var name = Cell("name").GetString().Trim();
                    if (numberText.Length == 0 && (name.Length == 0 || name.StartsWith("Итого", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("Всего", StringComparison.OrdinalIgnoreCase)))
                    { warnings.Add($"Строка Excel {i}: пропущена служебная строка итогов/пустой разделитель."); continue; }
                    try
                    {
                        var number = Number(Cell("number"));
                        if (number <= 0 || number != decimal.Truncate(number) || number > int.MaxValue || !ids.Add((int)number))
                            throw new InvalidDataException("N должен быть положительным целым и уникальным.");
                        var row = new SalesReportRow { RowNumber = (int)number, ExcelRowNumber = i, ProductName = name,
                            Characteristic = Cell("characteristic").GetString().Trim(), Quantity = Number(Cell("quantity")),
                            BasePrice = Number(Cell("price")), CurrentManualDiscountPercent = Number(Cell("discount"), true, true),
                            CurrentTotal = Number(Cell("total")), CurrentManualDiscountAmount = Optional("discountAmount"),
                            VatRate = Text("vatRate"), VatAmount = Optional("vatAmount"), RealizationDocument = Text("document") };
                        if (name.Length == 0 || row.Quantity <= 0 || row.BasePrice < 0 || row.CurrentTotal < 0
                            || row.CurrentManualDiscountPercent < 0 || row.CurrentManualDiscountPercent > 100)
                            throw new InvalidDataException("Название, количество, цена, скидка или сумма недопустимы; возвраты с отрицательным количеством не поддерживаются.");
                        Money.ToMinor(row.CurrentTotal); Money.ToMinor(row.BasePrice);
                        if (row.CurrentManualDiscountAmount is { } discountAmount) Money.ToMinor(discountAmount);
                        if (row.VatAmount is { } vatAmount) Money.ToMinor(vatAmount);
                        rows.Add(classifier.Classify(row, settings));
                    }
                    catch (Exception ex) when (ex is FormatException or OverflowException or InvalidOperationException or InvalidDataException)
                    { throw new InvalidDataException($"Лист «{sheet.Name}», строка Excel {i}: {ex.Message} Импорт остановлен, чтобы не потерять часть контрольной суммы.", ex); }
                }
                if (rows.Count == 0) throw new InvalidDataException("В отчёте не найдены строки продаж.");
                var mismatches = rows.Count(r => !calculator.Matches(r, settings.Rounding));
                if (mismatches > 0) warnings.Add(DiscountCalculationService.MismatchWarning + $" Несовпадений: {mismatches} из {rows.Count}.");
                return new(rows, path, warnings, mismatches == 0, rows.Count, mismatches);
            }
        }
        throw new InvalidDataException("Не найден заголовок отчёта: N, Номенклатура, Характеристика, Продано, Цена, % руч., Сумма (первые 100 строк каждого листа).");
    }
}
