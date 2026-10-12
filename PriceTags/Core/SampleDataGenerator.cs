using ClosedXML.Excel;
using FanShop.ReportAdjustment.Models;
using FanShop.ReportAdjustment.Services;

namespace FanShop.PriceTags;

public static class SampleDataGenerator
{
    public static void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        using (var book = new XLWorkbook())
        {
            var sheet = book.AddWorksheet("Товары");
            string[] headers = ["Штрихкод", "Артикул", "Номенклатура", "Характеристика"];
            for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
            string[][] products = [
                ["1111111111111", "ЦБ-00008440", "Футболка Jogel", "S"],
                ["1111111111112", "ЦБ-00008440", "Футболка Jogel", "M"],
                ["1111111111113", "ЦБ-00008440", "Футболка Jogel", "L"],
                ["2222222222222", "ЦБ-00009123", "Кружка Зенит", "MISC"],
                ["000123", "ЦБ-00000001", "Товар с ведущими нулями", "MISC"]
            ];
            for (var row = 0; row < products.Length; row++)
                for (var column = 0; column < 4; column++) sheet.Cell(row + 2, column + 1).Value = products[row][column];
            sheet.Column(1).Style.NumberFormat.Format = "@";
            sheet.Row(1).Style.Font.Bold = true; sheet.Columns().AdjustToContents();
            book.SaveAs(Path.Combine(directory, "catalog.xlsx"));
        }
        File.WriteAllLines(Path.Combine(directory, "scan.txt"), ["1111111111113", "1111111111113", "1111111111113", "1111111111112", "2222222222222", "2222222222222"]);
        var reports = Path.Combine(directory, "ReportAdjustment"); Directory.CreateDirectory(reports);
        var calculator = new DiscountCalculationService();
        SalesReportRow Row(int id, decimal price, string size = "MISC", decimal quantity = 1, decimal discount = 0, string name = "Товар")
            => new() { RowNumber = id, ProductName = name, Characteristic = size, Quantity = quantity, BasePrice = price,
                CurrentManualDiscountPercent = discount, CurrentTotal = calculator.Calculate(quantity, price, discount, DiscountRounding.MathematicalRound) };
        var cases = new (string Name, SalesReportRow[] Rows, decimal Amount)[]
        {
            ("A_MISC.xlsx", [Row(1, 1000)], 30),
            ("B_MISC_Clothing.xlsx", [Row(1, 1000), Row(2, 1000, "S")], 30),
            ("C_OneVsThree.xlsx", [Row(1, 1000), Row(2, 100), Row(3, 100), Row(4, 100)], 30),
            ("D_NoExact.xlsx", [Row(1, 100)], 2),
            ("E_Nonstandard.xlsx", [Row(1, 499, discount: 30.02m), Row(2, 1000)], 30),
            ("F_Quantity.xlsx", [Row(14, 150, quantity: 2), Row(21, 1060, name: "Нанесение Имя/фамилия")], 180),
            ("G_Large.xlsx", Enumerable.Range(1, 300).Select(i => Row(i, 100 + i % 20 * 10)).ToArray(), 180)
        };
        foreach (var item in cases) WriteReport(Path.Combine(reports, item.Name), item.Rows);
        File.WriteAllLines(Path.Combine(reports, "README.txt"), cases.Select(c => $"{c.Name}: сумма корректировки {c.Amount} ₽; строк {c.Rows.Length}."));
    }
    private static void WriteReport(string path, IReadOnlyList<SalesReportRow> rows)
    {
        using var book = new XLWorkbook(); var sheet = book.AddWorksheet("Продажи");
        string[] headers = ["N", "Номенклатура", "Характеристика", "Продано", "Цена", "% руч.", "Сумма руч.", "Сумма", "Ставка НДС", "Сумма НДС", "Документ реализации"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(4, i + 1).Value = headers[i];
        var index = 5;
        foreach (var row in rows)
        {
            sheet.Cell(index, 1).Value = row.RowNumber; sheet.Cell(index, 2).Value = row.ProductName;
            sheet.Cell(index, 3).Value = row.Characteristic; sheet.Cell(index, 4).Value = row.Quantity;
            sheet.Cell(index, 5).Value = row.BasePrice; sheet.Cell(index, 6).Value = row.CurrentManualDiscountPercent;
            sheet.Cell(index, 7).Value = row.Quantity * row.BasePrice - row.CurrentTotal;
            sheet.Cell(index++, 8).Value = row.CurrentTotal;
        }
        sheet.Cell(index, 2).Value = "Итого"; sheet.Cell(index, 8).Value = rows.Sum(r => r.CurrentTotal);
        sheet.Row(4).Style.Font.Bold = true; sheet.Columns().AdjustToContents(); book.SaveAs(path);
    }
}
