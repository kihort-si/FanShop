using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace FanShop.Replenishment;

public sealed class InventoryReportLoader
{
    public InventoryReport Load(string path, CancellationToken token = default)
    {
        if (new FileInfo(path).Length > 50 * 1024 * 1024) throw new InvalidDataException("Файл превышает 50 МБ. Выгрузите более короткий отчёт.");
        using var input = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(input)); input.Position = 0;
        using var book = new XLWorkbook(input);
        var items = new Dictionary<string, InventoryItem>();
        DateOnly? day = null;
        var found = false;
        foreach (var sheet in book.Worksheets)
        {
            var used = sheet.RangeUsed(); if (used is null) continue;
            if (used.LastRow().RowNumber() > 100000) throw new InvalidDataException("В отчёте больше 100 000 строк. Разделите выгрузку.");
            int article = 0, description = 0, opening = 0, incoming = 0, expense = 0, closing = 0, header = 0;
            foreach (var row in sheet.RowsUsed().Take(50))
            {
                foreach (var cell in row.CellsUsed())
                {
                    var text = cell.GetString().Trim();
                    var period = Regex.Match(text, @"Период\s*:\s*(\d{2}\.\d{2}\.\d{4})\s*-\s*(\d{2}\.\d{2}\.\d{4})", RegexOptions.IgnoreCase);
                    if (period.Success)
                    {
                        var start = DateOnly.ParseExact(period.Groups[1].Value, "dd.MM.yyyy", CultureInfo.InvariantCulture);
                        var end = DateOnly.ParseExact(period.Groups[2].Value, "dd.MM.yyyy", CultureInfo.InvariantCulture);
                        if (start != end) throw new InvalidDataException("Для довесов нужен отчёт за один день, с начала дня. Выгрузите каждый день отдельно.");
                        if (day is not null && day != start) throw new InvalidDataException("В книге найдены разные даты отчётов.");
                        day = start;
                    }
                    var name = InventoryKeys.Normalize(text); var column = cell.Address.ColumnNumber;
                    switch (name)
                    {
                        case "АРТИКУЛ": article = column; header = Math.Max(header, row.RowNumber()); break;
                        case "НОМЕНКЛАТУРА, ХАРАКТЕРИСТИКА": case "НОМЕНКЛАТУРА": description = column; header = Math.Max(header, row.RowNumber()); break;
                        case "НАЧАЛЬНЫЙ ОСТАТОК": opening = column; break;
                        case "ПРИХОД": incoming = column; break;
                        case "РАСХОД": expense = column; break;
                        case "КОНЕЧНЫЙ ОСТАТОК": closing = column; break;
                    }
                }
                if (article > 0 && description > 0 && opening > 0 && incoming > 0 && expense > 0 && closing > 0) break;
            }
            if (article == 0 || description == 0 || opening == 0 || incoming == 0 || expense == 0 || closing == 0) continue;
            found = true;
            string warehouse = "";
            foreach (var row in sheet.Rows(header + 1, used.LastRow().RowNumber()))
            {
                token.ThrowIfCancellationRequested();
                var code = row.Cell(article).GetFormattedString().Trim();
                var name = row.Cell(description).GetString().Trim();
                if (InventoryKeys.Normalize(code).StartsWith("ИТОГО", StringComparison.Ordinal)) break;
                if (code.Length == 0 && name.Length == 0) continue;
                if (name.Length == 0)
                {
                    // 1C warehouse totals occupy a merged cell across article + description columns.
                    var merged = row.Cell(article).MergedRange();
                    if (code.Length > 0 && merged is not null && merged.LastColumn().ColumnNumber() >= description)
                    { warehouse = code; continue; }
                    throw new InvalidDataException($"Лист «{sheet.Name}», строка {row.RowNumber()}: не распознана строка товара или склада.");
                }
                if (code.Length == 0)
                    throw new InvalidDataException($"Лист «{sheet.Name}», строка {row.RowNumber()}: отсутствует артикул.");
                decimal Read(int col)
                {
                    var cell = row.Cell(col);
                    if (cell.IsEmpty()) return 0;
                    if (cell.DataType == XLDataType.Number) return cell.GetValue<decimal>();
                    var value = Regex.Replace(cell.GetString(), @"\s", "").Replace(',', '.');
                    if (value is "" or "-" or "—") return 0;
                    if (decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)) return number;
                    throw new InvalidDataException($"Лист «{sheet.Name}», строка {row.RowNumber()}, столбец {col}: неверное количество «{cell.GetString()}».");
                }
                var item = new InventoryItem(warehouse, code, name, Read(opening), Read(incoming), Read(expense), Read(closing));
                if (!items.TryAdd(InventoryKeys.Normalize(warehouse) + "|" + item.Key, item)) throw new InvalidDataException($"Повторная строка товара «{code}, {name}» на складе «{warehouse}». Импорт отменён, чтобы не задвоить расход.");
            }
        }
        if (!found || items.Count == 0) throw new InvalidDataException("Не найдена таблица «Ведомость по товарам на складах» с остатками, приходом и расходом.");
        if (day is null) throw new InvalidDataException("Не найден период отчёта. Ожидается «Период: ДД.ММ.ГГГГ - ДД.ММ.ГГГГ».");
        return new(day.Value, Path.GetFileName(path), hash, InventoryAggregation.Merge(items.Values));
    }
}
