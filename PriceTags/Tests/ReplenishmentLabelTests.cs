using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FanShop.PriceTags.Models;
using FanShop.PriceTags.Services;
using FanShop.Replenishment;
using FanShop.View;
using FanShop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FanShop.PriceTags.Tests;

public class ReplenishmentLabelTests
{
    private static readonly DateOnly Day=new(2026,11,3);
    private static InventoryItem Item(string size="M")=>new("","000123","Футболка, "+size,10,0,3,7);
    private static InventoryReport Report()=>new(Day,"report.xlsx","first",[Item(),Item("XL")]);
    [Fact]
    public void ReservationsAndPrintedCountsPersistAndOnlyNewCompletionBecomesAvailable()
    {
        using var f=new ReplenishmentFixture();Guid id;
        using(var store=new ReplenishmentStore(f.Data))
        {
            store.Import(Report());store.Complete(Day,Item().Key,2);
            var batch=store.ReserveLabels(Day,new Dictionary<string,int>{{Item().Key,2}});id=batch.Id;
            Assert.Equal(0,store.Rows(Day).Single(r=>r.Key==Item().Key).LabelsAvailable);
            Assert.Throws<InvalidOperationException>(()=>store.ReserveLabels(Day,new Dictionary<string,int>{{Item().Key,1}}));
            store.MarkLabelsSent(Day,id);
            Assert.Equal(0,store.Rows(Day).Single(r=>r.Key==Item().Key).Printed);
        }
        using(var store=new ReplenishmentStore(f.Data))
        {
            Assert.Equal(2,store.Rows(Day).Single(r=>r.Key==Item().Key).Reserved);
            store.ConfirmLabelsPrinted(Day,id);
            var row=store.Rows(Day).Single(r=>r.Key==Item().Key);Assert.Equal(2,row.Printed);Assert.Equal(0,row.Reserved);
            Assert.Throws<InvalidOperationException>(()=>store.ConfirmLabelsPrinted(Day,id));
            store.Complete(Day,Item().Key,1);
            Assert.Equal(1,store.Rows(Day).Single(r=>r.Key==Item().Key).LabelsAvailable);
            store.Import(Report() with{Hash="second"});
            Assert.Equal(2,store.Rows(Day).Single(r=>r.Key==Item().Key).Printed);
            store.Import(Report() with{Day=Day.AddDays(1),Hash="nextday"});
            store.Complete(Day.AddDays(1),Item().Key,1);
            Assert.Equal(1,store.Rows(Day.AddDays(1)).Single(r=>r.Key==Item().Key).LabelsAvailable);
        }
    }
    [Fact]
    public void PrintingHistoryRemainsVisibleAfterUndoAndZeroExpense()
    {
        using var f=new ReplenishmentFixture();using var store=new ReplenishmentStore(f.Data);
        store.Import(Report());store.Complete(Day,Item().Key,3);
        var completion=store.Load(Day).Events.Single();
        var batch=store.ReserveLabels(Day,new Dictionary<string,int>{{Item().Key,3}});
        Assert.Equal("Не подтверждена",batch.PrintedText);
        store.ConfirmLabelsPrinted(Day,batch.Id);store.Undo(Day,completion.Id);
        store.Import(Report() with{Hash="returns",Items=[Item() with{Expense=0},Item("XL")]});
        var row=store.Rows(Day).Single(r=>r.Key==Item().Key);
        Assert.Equal(0,row.Completed);Assert.Equal(3,row.Printed);Assert.Equal(0,row.LabelsAvailable);
    }
    [Fact]
    public void CancelReleasesReservationAndCannotUndoConfirmedPrinting()
    {
        using var f=new ReplenishmentFixture();using var store=new ReplenishmentStore(f.Data);
        store.Import(Report());store.Complete(Day,Item().Key,3);
        var batch=store.ReserveLabels(Day,new Dictionary<string,int>{{Item().Key,2}});
        store.CancelLabelBatch(Day,batch.Id);
        Assert.Equal(3,store.Rows(Day).Single(r=>r.Key==Item().Key).LabelsAvailable);
        batch=store.ReserveLabels(Day,new Dictionary<string,int>{{Item().Key,2}});store.ConfirmLabelsPrinted(Day,batch.Id);
        Assert.Throws<InvalidOperationException>(()=>store.CancelLabelBatch(Day,batch.Id));
        Assert.Throws<InvalidOperationException>(()=>store.ReserveLabels(Day,new Dictionary<string,int>{{Item("XL").Key,1}}));
    }
    [Fact]
    public void VersionOneWarehouseHistoryMigratesWithoutLosingCompletedWork()
    {
        using var f=new ReplenishmentFixture();Directory.CreateDirectory(f.Data);
        var a=Item() with{Warehouse="Зал 1"};var b=Item() with{Warehouse="Зал 2"};
        var state=new ReplenishmentDay {Format=1,Day=Day,Items=[a,b],Events=[new(Guid.NewGuid(),DateTimeOffset.Now,a,1),new(Guid.NewGuid(),DateTimeOffset.Now,b,2)]};
        var path=Path.Combine(f.Data,"day-2026-11-03.json");
        File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(state));
        using var store=new ReplenishmentStore(f.Data);
        var row=Assert.Single(store.Rows(Day));Assert.Equal(6,row.Expense);Assert.Equal(3,row.Completed);Assert.Equal(3,row.Remaining);
        Assert.True(File.Exists(path+".v1-backup"));
        store.Complete(Day,row.Key,1);Assert.Equal(2,store.Load(Day).Format);
        Assert.Equal(4,Assert.Single(store.Rows(Day)).Completed);
    }
    [Fact]
    public void ExternalQueueKeepsScansRestoresBatchMetadataAndRefusesToReplaceExistingQueue()
    {
        using var f=new ReplenishmentFixture();using var storage=new LocalStorage(Path.Combine(f.Root,"tags"));
        var catalog=new ProductCatalogService();catalog.Replace([new ProductInfo("keep","old","Товар","M")]);var session=new ScanSessionService(catalog,storage,NullLogger<ScanSessionService>.Instance);
        session.ProcessBarcode("keep",ScanSource.File);
        var queue=new TransferQueueService(session,storage,new Keyboard(),NullLogger<TransferQueueService>.Instance);
        var batch=Guid.NewGuid();var item=new TransferItem(Guid.NewGuid(),"000123","Футболка","M",2,SourceBatchId:batch,SourceDay:Day);
        queue.PrepareExternal([item]);Assert.Single(session.Snapshot());Assert.True(session.IsFrozen);
        Assert.Throws<InvalidOperationException>(()=>queue.PrepareExternal([item with{Id=Guid.NewGuid()}]));
        var restored=new TransferQueueService(session,storage,new Keyboard(),NullLogger<TransferQueueService>.Instance);restored.Restore();
        Assert.Equal(batch,Assert.Single(restored.Snapshot()).SourceBatchId);Assert.Equal(2,restored.Snapshot()[0].Quantity);
        restored.CloseQueue();Assert.Single(session.Snapshot());Assert.False(session.IsFrozen);
    }
    [AvaloniaFact]
    public async Task SelectionHandsOffOnlyChosenQuantityAndCopiesOneCell()
    {
        using var f=new ReplenishmentFixture();var store=new ReplenishmentStore(f.Data);
        ReplenishmentLabelBatch? sent=null;
        var vm=new ReplenishmentViewModel(store,()=>Task.CompletedTask,(batch,_)=>{sent=batch;return Task.CompletedTask;});
        await vm.ImportFileAsync(f.Book(Day,[Item(),Item("XL")]));
        foreach(var size in new[]{"M","XL"}) {vm.SelectedRow=vm.Rows.Single(r=>r.Characteristic==size);await vm.CompleteRemainingCommand.ExecuteAsync(null);}
        var choice=vm.LabelChoices.Single(r=>r.Characteristic=="M");choice.IsSelected=true;choice.QuantityToPrint=2;
        await vm.SendLabelsCommand.ExecuteAsync(null);Assert.False(vm.IsError);
        Assert.NotNull(sent);Assert.Equal(2,Assert.Single(sent.Lines).Quantity);Assert.Equal("M",sent.Lines[0].Characteristic);
        Assert.Equal(2,vm.LabelChoices.Single(r=>r.Characteristic=="M").Reserved);Assert.Equal(0,vm.LabelChoices.Single(r=>r.Characteristic=="XL").Reserved);
        var view=new ReplenishmentControl {DataContext=vm};var window=new Window {Content=view,Width=1500,Height=950};window.Show();window.UpdateLayout();
        var grid=view.FindControl<DataGrid>("ReplenishmentGrid")!;grid.SelectedItem=vm.Rows.First();grid.CurrentColumn=grid.Columns[0];grid.Focus();
        window.KeyPressQwerty(PhysicalKey.C,RawInputModifiers.Control);window.KeyReleaseQwerty(PhysicalKey.C,RawInputModifiers.Control);
        Assert.Equal("000123",await window.Clipboard!.GetTextAsync());
        view.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex=1;window.UpdateLayout();
        window.CaptureRenderedFrame()?.Save(Path.Combine(Path.GetTempPath(),"fanshop-replenishment-labels.png"));
        window.Close();await vm.ShutdownAsync();
    }
    [AvaloniaFact]
    public async Task FailedHandoffKeepsReservationForReviewInsteadOfAllowingDuplicatePrinting()
    {
        using var f=new ReplenishmentFixture();var store=new ReplenishmentStore(f.Data);
        var vm=new ReplenishmentViewModel(store,()=>Task.CompletedTask,(_,_)=>throw new IOException("Test handoff failure"));
        await vm.ImportFileAsync(f.Book(Day,[Item()]));vm.SelectedRow=Assert.Single(vm.Rows);await vm.CompleteRemainingCommand.ExecuteAsync(null);
        var choice=Assert.Single(vm.LabelChoices);choice.IsSelected=true;choice.QuantityToPrint=3;
        await vm.SendLabelsCommand.ExecuteAsync(null);Assert.True(vm.IsError);
        Assert.Equal(3,Assert.Single(vm.LabelChoices).Reserved);Assert.Equal(0,vm.LabelChoices[0].Available);
        Assert.Single(store.Load(Day).LabelBatches);await vm.ShutdownAsync();
    }
    private sealed class Keyboard:IOneCKeyboard
    {
        public bool IsSupported=>true;public void CaptureTarget(){}public void ValidateTarget(){}
        public void TypeText(string text,CancellationToken token){}public void Enter(CancellationToken token){}
    }
}
