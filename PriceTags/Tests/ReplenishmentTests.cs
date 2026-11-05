using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ClosedXML.Excel;
using FanShop.Replenishment;
using FanShop.View;
using FanShop.ViewModels;
using Xunit;

namespace FanShop.PriceTags.Tests;

public class ReplenishmentTests
{
    private static readonly DateOnly Day = new(2026,11,3);
    private static InventoryItem Item(decimal expense = 3, string size = "M", string warehouse = "Зал", string article = "000123", string name = "Футболка")
        => new(warehouse, article, name + ", " + size, 10, 0, expense, 10-expense);
    private static InventoryReport Report(string hash, params InventoryItem[] items) => new(Day, hash + ".xlsx", hash, InventoryAggregation.Merge(items));

    [Fact]
    public void LoaderReadsMerged1CHeadersAndSkipsWarehouseAndOverallTotals()
    {
        using var f = new ReplenishmentFixture();
        var path = f.Book(Day, [Item(), Item(0,"XL"), Item(2,"M","Склад 2")]);
        var report = new InventoryReportLoader().Load(path);
        Assert.Equal(Day, report.Day); Assert.Equal(2, report.Items.Length);
        Assert.Equal(5m, report.Items.Sum(i=>i.Expense));
        Assert.All(report.Items,i=>Assert.Empty(i.Warehouse));
        Assert.All(report.Items, i=>Assert.Equal("000123",i.Article));
        Assert.Equal(64, report.Hash.Length);
    }
    [Fact]
    public void CumulativeReportsSubtractCompletedInsteadOfAddingDemandAndSurviveRestart()
    {
        using var f = new ReplenishmentFixture();
        using(var store = new ReplenishmentStore(f.Data))
        {
            store.Import(Report("first",Item()));
            store.Complete(Day,Item().Key,2);
            Assert.Equal(1, Assert.Single(store.Rows(Day)).Remaining);
            store.Import(Report("second",Item(5)));
            var row=Assert.Single(store.Rows(Day)); Assert.Equal(2,row.Completed);Assert.Equal(3,row.Remaining);
            store.Import(Report("second",Item(5)));
            Assert.Equal(2,store.Load(Day).Reports.Count); Assert.Equal(3,Assert.Single(store.Rows(Day)).Remaining);
            store.Complete(Day,Item().Key,3);
        }
        using(var store = new ReplenishmentStore(f.Data))
        {
            var row=Assert.Single(store.Rows(Day)); Assert.Equal(5,row.Completed);Assert.Equal(0,row.Remaining);
            store.Import(Report("third",Item(5)));
            Assert.Equal(0,Assert.Single(store.Rows(Day)).Remaining);
            store.Import(new(Day.AddDays(1),"tomorrow.xlsx","tomorrow",[Item(5)]));
            Assert.Equal(5,Assert.Single(store.Rows(Day.AddDays(1))).Remaining);
            Assert.Equal(5,Assert.Single(store.Rows(Day)).Completed);
        }
    }
    [Fact]
    public void SizesStayIndependentWhileWarehouseRowsMergeAndHistorySurvives()
    {
        using var f = new ReplenishmentFixture(); using var store = new ReplenishmentStore(f.Data);
        store.Import(Report("first",Item(),Item(4,"XL"),Item(2,"M","Склад 2")));
        store.Complete(Day,Item().Key,3);
        var rows=store.Rows(Day); Assert.Equal(2,rows.Length);
        Assert.Equal(4,rows.Single(r=>r.Characteristic=="XL").Remaining);
        Assert.Equal(2,rows.Single(r=>r.Key==Item().Key).Remaining);
        store.Import(Report("new-hall",Item(0,"XL")));
        rows=store.Rows(Day);
        Assert.Equal(3,rows.Single(r=>r.Key==Item().Key).Completed);
        Assert.Equal(2,rows.Single(r=>r.Key==Item().Key).Remaining);
        Assert.Equal(2,rows.Single(r=>r.Key==Item().Key).Remaining);
    }
    [Fact]
    public void ExclusionsPersistAcrossDaysAndDoNotEraseCompletedWork()
    {
        using var f = new ReplenishmentFixture();
        using(var store = new ReplenishmentStore(f.Data))
        {
            store.Import(Report("first",Item(),Item(4,"XL"),Item(2,article:"scarf",name:"Шарф матчевый")));
            store.Complete(Day,Item().Key,1);
            store.AddRule(ExclusionKind.Article,"000123");store.AddRule(ExclusionKind.NameContains,"ШАРФ");
            Assert.All(store.Rows(Day),r=>Assert.Equal(0,r.Remaining));
            Assert.Equal(1,store.Load(Day).Events.Sum(e=>e.Quantity));
        }
        using(var store = new ReplenishmentStore(f.Data))
        {
            Assert.Equal(2,store.Rules().Length);
            store.Import(new(Day.AddDays(1),"next.xlsx","next",[Item()]));
            Assert.Equal(0,Assert.Single(store.Rows(Day.AddDays(1))).Remaining);
            store.RemoveRule(store.Rules().Single(r=>r.Kind==ExclusionKind.Article).Id);
            Assert.Equal(2,store.Rows(Day).Single(r=>r.Key==Item().Key).Remaining);
        }
    }
    [Fact]
    public void PartialCompletionAndUndoAreAuditedAndCannotOverfillOrUndoTwice()
    {
        using var f = new ReplenishmentFixture();using var store = new ReplenishmentStore(f.Data);
        store.Import(Report("first",Item()));
        Assert.Throws<InvalidOperationException>(()=>store.Complete(Day,Item().Key,4));
        Assert.Throws<InvalidOperationException>(()=>store.Complete(Day,Item().Key,0));
        store.Complete(Day,Item().Key,2); var id=Assert.Single(store.Load(Day).Events).Id;
        store.Undo(Day,id); Assert.Equal(3,Assert.Single(store.Rows(Day)).Remaining);
        Assert.Equal(2,store.Load(Day).Events.Count);
        Assert.Equal(0,store.Load(Day).Events.Sum(e=>e.Quantity));
        Assert.Throws<InvalidOperationException>(()=>store.Undo(Day,id));
    }
    [Fact]
    public void LowerCumulativeExpenseDoesNotEraseCompletedAndLaterGrowthOnlyAddsPending()
    {
        using var f = new ReplenishmentFixture(); using var store=new ReplenishmentStore(f.Data);
        store.Import(Report("first",Item(3))); store.Complete(Day,Item().Key,2);
        store.Import(Report("return",Item(1)));
        var row=Assert.Single(store.Rows(Day));Assert.Equal(2,row.Completed);Assert.Equal(0,row.Remaining);
        store.Import(Report("later",Item(4)));
        row=Assert.Single(store.Rows(Day));Assert.Equal(2,row.Completed);Assert.Equal(2,row.Remaining);
        Assert.Single(store.Load(Day).Events);
    }
    [Fact]
    public void MissingRowsDoNotLosePendingWork()
    {
        using var f = new ReplenishmentFixture(); using var store = new ReplenishmentStore(f.Data);
        store.Import(Report("first",Item(),Item(4,"XL")));
        store.Import(Report("second",Item(4,"XL")));
        Assert.Equal(3,store.Rows(Day).Single(r=>r.Characteristic=="M").Remaining);
        Assert.Equal(2,store.Load(Day).Reports.Count);
    }

    [Fact]
    public void CorruptStateIsNeverSilentlyOverwrittenAndOnlyOneInstanceCanOwnData()
    {
        using var f = new ReplenishmentFixture();using var store=new ReplenishmentStore(f.Data);
        Assert.Throws<IOException>(()=>new ReplenishmentStore(f.Data));
        var path=Path.Combine(f.Data,"day-2026-11-03.json");File.WriteAllText(path,"broken");
        Assert.ThrowsAny<Exception>(()=>store.Import(Report("first",Item())));
        Assert.Equal("broken",File.ReadAllText(path));
    }
    [Fact]
    public void ExportIncludesEntireDayAuditAndTextArticles()
    {
        using var f = new ReplenishmentFixture();using var store=new ReplenishmentStore(f.Data);
        store.Import(Report("first",Item(),Item(4,"XL")));store.Complete(Day,Item().Key,2);
        var path=Path.Combine(f.Root,"result.xlsx");
        ReplenishmentExport.Save(path,store.Load(Day),store.Rows(Day));
        using var book=new XLWorkbook(path);Assert.Equal(4,book.Worksheets.Count);
        var sheet=book.Worksheet("Итог за день");Assert.Equal(2,sheet.Cell(2,2).GetValue<decimal>());
        Assert.Equal("000123",sheet.Cell(5,1).GetString());Assert.Equal(2,sheet.Cell(5,8).GetValue<decimal>());
        Assert.Equal(7,book.Worksheet("Загруженные отчёты").Cell(2,4).GetValue<decimal>());
    }
    [Fact]
    public void InvalidPeriodAndDuplicatePositionsAreRejectedBeforeStateChanges()
    {
        using var f = new ReplenishmentFixture();
        var path=f.Book(Day,[Item()]);
        using(var book=new XLWorkbook(path)) { book.Worksheet(1).Cell(4,5).Value="Период: 03.11.2026 - 04.11.2026";book.Save(); }
        Assert.Throws<InvalidDataException>(()=>new InventoryReportLoader().Load(path));
        var duplicate=f.Book(Day,[Item(),Item()]);
        Assert.Throws<InvalidDataException>(()=>new InventoryReportLoader().Load(duplicate));
    }
    [AvaloniaFact]
    public async Task IntegratedScreenImportsMarksAndShowsUpdatedTotals()
    {
        using var f=new ReplenishmentFixture();
        var vm=new ReplenishmentViewModel(new ReplenishmentStore(f.Data));
        await vm.ImportFileAsync(f.Book(Day,[Item()]));
        Assert.False(vm.IsError);Assert.Single(vm.Rows);
        vm.SelectedRow=vm.Rows[0];vm.Quantity=2;await vm.CompleteCommand.ExecuteAsync(null);
        Assert.Equal(2,Assert.Single(vm.Rows).Completed);Assert.Equal(1,Assert.Single(vm.Rows).Remaining);
        var view=new ReplenishmentControl {DataContext=vm};var window=new Window {Content=view,Width=1500,Height=900};
        window.Show();window.UpdateLayout();
        Assert.Contains(view.GetVisualDescendants().OfType<Button>(),b=>Equals(b.Content,"Довешено всё оставшееся"));
        var grid=view.FindControl<DataGrid>("ReplenishmentGrid")!;Assert.True(grid.Bounds.Height>100);
        window.CaptureRenderedFrame()?.Save(Path.Combine(Path.GetTempPath(),"fanshop-replenishment-ui.png"));
        window.Close();await vm.ShutdownAsync();
    }
}

internal sealed class ReplenishmentFixture : IDisposable
{
    public string Root {get;}=Path.Combine(Path.GetTempPath(),"fanshop-replenishment-"+Guid.NewGuid().ToString("N"));
    public string Data=>Path.Combine(Root,"data");
    public ReplenishmentFixture()=>Directory.CreateDirectory(Root);
    public string Book(DateOnly day,IReadOnlyCollection<InventoryItem> items)
    {
        var path=Path.Combine(Root,Guid.NewGuid().ToString("N")+".xlsx");
        using var book=new XLWorkbook();var sheet=book.AddWorksheet("Ведомость");
        sheet.Cell(2,1).Value="Ведомость по товарам на складах";
        sheet.Cell(4,5).Value=$"Период: {day:dd.MM.yyyy} - {day:dd.MM.yyyy}";
        sheet.Range(7,1,7,13).Merge();sheet.Cell(7,1).Value="Склад";
        sheet.Range(8,1,8,3).Merge();sheet.Cell(8,1).Value="Артикул";
        sheet.Range(8,4,8,9).Merge();sheet.Cell(8,4).Value="Номенклатура, Характеристика";
        string[] headers=["Начальный остаток","Приход","Расход","Конечный остаток"];
        for(var col=14;col<=17;col++) {sheet.Range(7,col,8,col).Merge();sheet.Cell(7,col).Value=headers[col-14];}
        var row=9;
        foreach(var warehouse in items.GroupBy(i=>i.Warehouse))
        {
            sheet.Range(row,1,row,13).Merge();sheet.Cell(row,1).Value=warehouse.Key;sheet.Cell(row,16).Value=warehouse.Sum(i=>i.Expense);row++;
            foreach(var item in warehouse)
            {
                sheet.Range(row,1,row,3).Merge();sheet.Cell(row,1).Value=item.Article;
                sheet.Range(row,4,row,9).Merge();sheet.Cell(row,4).Value=item.Description;
                sheet.Cell(row,14).Value=item.Opening;sheet.Cell(row,15).Value=item.Incoming;
                sheet.Cell(row,16).Value=item.Expense;sheet.Cell(row,17).Value=item.Closing;row++;
            }
        }
        sheet.Cell(row,1).Value="Итого";sheet.Cell(row,16).Value=items.Sum(i=>i.Expense);
        book.SaveAs(path);return path;
    }
    public void Dispose()=>Directory.Delete(Root,true);
}
