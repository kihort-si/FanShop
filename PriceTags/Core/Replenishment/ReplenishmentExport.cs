using ClosedXML.Excel;
namespace FanShop.Replenishment;
public static class ReplenishmentExport
{
    public static void Save(string path, ReplenishmentDay day, IReadOnlyCollection<ReplenishmentRow> rows)
    {
        using var book = new XLWorkbook();
        var sheet=book.Worksheets.Add("Итог за день");
        sheet.Cell(1,1).Value=$"Довесы за {day.Day:dd.MM.yyyy}";
        sheet.Cell(2,1).Value="Всего довешено";sheet.Cell(2,2).Value=day.Events.Sum(e=>e.Quantity);
        Headers(sheet,4,["Артикул","Номенклатура","Характеристика","Начальный остаток","Приход","Расход","Конечный остаток","Довешено","Осталось довесить","Напечатано ценников","В партиях","Доступно ценников","Исключение"]);
        var n=5;
        foreach(var row in rows)
        {
            Text(sheet,n,1,row.Article);sheet.Cell(n,2).Value=row.Name;sheet.Cell(n,3).Value=row.Characteristic;
            sheet.Cell(n,4).Value=row.Opening;sheet.Cell(n,5).Value=row.Incoming;sheet.Cell(n,6).Value=row.Expense;
            sheet.Cell(n,7).Value=row.Closing;sheet.Cell(n,8).Value=row.Completed;sheet.Cell(n,9).Value=row.Remaining;
            sheet.Cell(n,10).Value=row.Printed;sheet.Cell(n,11).Value=row.Reserved;sheet.Cell(n,12).Value=row.LabelsAvailable;sheet.Cell(n,13).Value=row.Exclusion;n++;
        }
        Format(sheet,4,n-1,13);
        var history=book.Worksheets.Add("Журнал довесов");Headers(history,1,["Время","Действие","Артикул","Товар и характеристика","Количество"]);n=2;
        foreach(var item in day.Events)
        {
            history.Cell(n,1).Value=item.Time.ToString("dd.MM.yyyy HH:mm:ss zzz");history.Cell(n,2).Value=item.Action;
            Text(history,n,3,item.Item.Article);history.Cell(n,4).Value=item.Item.Description;history.Cell(n,5).Value=item.Quantity;n++;
        }
        Format(history,1,n-1,5);
        var reports=book.Worksheets.Add("Загруженные отчёты");Headers(reports,1,["Время загрузки","Файл","Товарных строк","Расход в отчёте"]);n=2;
        foreach(var report in day.Reports)
        {
            reports.Cell(n,1).Value=report.Time.ToString("dd.MM.yyyy HH:mm:ss zzz");reports.Cell(n,2).Value=report.FileName;
            reports.Cell(n,3).Value=report.ItemCount;reports.Cell(n,4).Value=report.Expense;n++;
        }
        Format(reports,1,n-1,4);
        var labels=book.Worksheets.Add("Ценники из довесов");Headers(labels,1,["Партия","Создана","Состояние","Артикул","Номенклатура","Характеристика","Количество","Передана в раздел ценников","Печать подтверждена","Отменена"]);n=2;
        foreach(var batch in day.LabelBatches)
            foreach(var line in batch.Lines)
            {
                labels.Cell(n,1).Value=batch.Number;labels.Cell(n,2).Value=batch.Created.ToString("dd.MM.yyyy HH:mm:ss zzz");labels.Cell(n,3).Value=batch.Status;
                Text(labels,n,4,line.Article);labels.Cell(n,5).Value=line.Name;labels.Cell(n,6).Value=line.Characteristic;labels.Cell(n,7).Value=line.Quantity;
                labels.Cell(n,8).Value=batch.Sent?.ToString("dd.MM.yyyy HH:mm:ss zzz")??"";labels.Cell(n,9).Value=batch.Printed?.ToString("dd.MM.yyyy HH:mm:ss zzz")??"";
                labels.Cell(n,10).Value=batch.Cancelled?.ToString("dd.MM.yyyy HH:mm:ss zzz")??"";n++;
            }
        Format(labels,1,n-1,10);book.SaveAs(path);
    }
    private static void Text(IXLWorksheet sheet,int row,int col,string value) {sheet.Cell(row,col).Style.NumberFormat.Format="@";sheet.Cell(row,col).Value=value;}
    private static void Headers(IXLWorksheet sheet,int row,string[] headers) {for(var i=0;i<headers.Length;i++)sheet.Cell(row,i+1).Value=headers[i];}
    private static void Format(IXLWorksheet sheet,int header,int last,int columns)
    {
        sheet.Row(header).Style.Font.Bold=true;sheet.Row(header).Style.Fill.BackgroundColor=XLColor.FromHtml("#D7EAF5");
        sheet.Range(header,1,Math.Max(header,last),columns).SetAutoFilter();sheet.SheetView.FreezeRows(header);sheet.Columns(1,columns).AdjustToContents(8,60);
    }
}
