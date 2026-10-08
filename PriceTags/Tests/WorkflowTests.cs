using ClosedXML.Excel;
using FanShop.PriceTags.Models;
using FanShop.PriceTags.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FanShop.PriceTags.Tests;

public sealed class WorkflowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fanshop-tests-" + Guid.NewGuid());
    private readonly LocalStorage _storage;
    private readonly ProductCatalogService _catalog = new();
    private readonly ScanSessionService _session;
    private readonly ProductInfo[] _products = [new("001", "ЦБ-00008440", "Футболка Jogel", "L"), new("002", "ЦБ-00008440", "Футболка Jogel", "M"), new("003", "ЦБ-00009123", "Кружка Зенит", "MISC")];
    public WorkflowTests() { _storage = new(_root); _catalog.Replace(_products); _session = new(_catalog, _storage, NullLogger<ScanSessionService>.Instance); }
    public void Dispose() { _storage.Dispose(); Directory.Delete(_root, true); }
    [Fact] public void CatalogNormalizesAndFindsExactBarcode()
    {
        _catalog.Replace([new(" 0000123 ", " ЦБ-1 ", " Название ", " MISC ")]);
        Assert.Equal("0000123", _catalog.Find(" 0000123 ")!.Barcode); Assert.Equal("ЦБ-1", _catalog.Find("0000123")!.Article); Assert.Null(_catalog.Find("123"));
    }
    [Fact] public void ConflictingCatalogLeavesPreviousCatalogIntact()
    {
        Assert.Throws<InvalidDataException>(() => _catalog.Replace([_products[0], _products[0] with { Article = "Другой" }]));
        Assert.Equal(_products[0], _catalog.Find("001"));
    }
    [Fact] public void RepeatsAndSizesAggregateSeparately()
    {
        foreach (var code in new[] { "001", "001", "001", "002", "003", "003" }) _session.ProcessBarcode(code, ScanSource.File);
        Assert.Equal(6, _session.Snapshot().Length); var totals = _session.Aggregate(); Assert.Equal(3, totals.Length);
        Assert.Equal(3, totals.Single(p => p.Characteristic == "L").Quantity); Assert.Equal(1, totals.Single(p => p.Characteristic == "M").Quantity); Assert.Equal(2, totals.Single(p => p.Characteristic == "MISC").Quantity);
    }
    [Fact] public void UnknownBlocksQueueUntilExplicitRemoval()
    {
        _session.ProcessBarcode("001", ScanSource.File); var unknown = _session.ProcessBarcode("999", ScanSource.Network);
        Assert.False(unknown.Found); Assert.Null(unknown.Product); Assert.Throws<InvalidOperationException>(() => _session.FreezeForTransfer());
        _session.Remove(unknown.Id); Assert.Single(_session.FreezeForTransfer());
    }
    [Fact] public void UndoRecalculatesAndPersists()
    {
        _session.ProcessBarcode("001", ScanSource.File); _session.ProcessBarcode("001", ScanSource.Network);
        Assert.True(_session.Undo()); Assert.Equal(1, _session.Aggregate().Single().Quantity); Assert.Single(_session.ReadSaved()!);
        _session.Undo(); Assert.False(_session.Undo()); Assert.Empty(_session.Aggregate());
    }
    [Fact] public void RequestRetryIsIdempotentEvenAfterUndo()
    {
        var id = Guid.NewGuid(); var first = _session.ProcessBarcode("001", ScanSource.Network, id);
        Assert.Equal(first, _session.ProcessBarcode(" 001 ", ScanSource.Network, id)); Assert.Single(_session.Snapshot());
        _session.Undo(); _session.ProcessBarcode("001", ScanSource.Network, id); Assert.Empty(_session.Snapshot());
        Assert.Throws<InvalidDataException>(() => _session.ProcessBarcode("002", ScanSource.Network, id));
    }
    [Fact] public void IdempotencySurvivesRestartAndRemovedScans()
    {
        var id=Guid.NewGuid(); _session.ProcessBarcode("001",ScanSource.Network,id); _session.Undo();
        var recovered=new ScanSessionService(_catalog,_storage,NullLogger<ScanSessionService>.Instance);
        recovered.Restore(recovered.ReadSaved()!); recovered.ProcessBarcode("001",ScanSource.Network,id);
        Assert.Empty(recovered.Snapshot());
    }
    [Fact] public async Task TxtUsesSameLookupAndPreservesSourceAndZeros()
    {
        var path = Path.Combine(_root, "scan.txt"); await File.WriteAllTextAsync(path, " 001 \n\n002\r\n002\n 999 \n");
        var importer = new TxtImportService(_session, NullLogger<TxtImportService>.Instance);
        Assert.Equal(4, await importer.ImportAsync(path)); Assert.All(_session.Snapshot(), r => Assert.Equal(ScanSource.File, r.Source));
        Assert.Equal("001", _session.Snapshot()[0].Barcode); Assert.Single(_session.Snapshot(), r => !r.Found);
    }
    [Fact] public async Task ConcurrentScansAreNotLost()
    {
        await Task.WhenAll(Enumerable.Range(0, 80).Select(_ => Task.Run(() => _session.ProcessBarcode("001", ScanSource.Network))));
        Assert.Equal(80, _session.Snapshot().Length); Assert.Equal(80, _session.ReadSaved()!.Length);
    }
    [Fact] public void FrozenQueueRejectsCatalogAndSessionChanges()
    {
        _session.ProcessBarcode("001", ScanSource.File); var transfer = CreateTransfer(new FakeKeyboard()); transfer.Prepare();
        Assert.Throws<InvalidOperationException>(() => _session.ProcessBarcode("002", ScanSource.File)); Assert.Throws<InvalidOperationException>(() => _session.Clear());
        Assert.Throws<InvalidOperationException>(() => _session.ReplaceCatalog(_products)); Assert.Single(transfer.Snapshot());
        transfer.CloseQueue(); _session.ProcessBarcode("002", ScanSource.File); Assert.Equal(2, _session.Snapshot().Length);
    }
    [Fact] public async Task TransferSequenceIsExactAndCompletionOccursAfterFinalEnter()
    {
        _session.ProcessBarcode("001", ScanSource.File); _session.ProcessBarcode("001", ScanSource.File);
        var keyboard = new FakeKeyboard(); var transfer = CreateTransfer(keyboard); transfer.Prepare();
        await transfer.RunAsync(new(100, 100, 100, 100, 100));
        Assert.Equal(new[] { "ЦБ-00008440", "ENTER", "L", "ENTER", "2", "ENTER" }, keyboard.Sent);
        Assert.Equal(TransferStatus.Completed, transfer.Snapshot().Single().Status);
    }
    [Fact] public async Task InterruptedRowRequiresReviewAndResumeSkipsCompletedRows()
    {
        _session.ProcessBarcode("001", ScanSource.File); _session.ProcessBarcode("003", ScanSource.File);
        var keyboard = new FakeKeyboard(); var transfer = CreateTransfer(keyboard); transfer.Prepare();
        keyboard.AfterText = text => { if (text == "ЦБ-00009123") transfer.Stop(); };
        await transfer.RunAsync(new(100,100,100,100,100));
        Assert.Equal(TransferStatus.Completed, transfer.Snapshot()[0].Status); Assert.Equal(TransferStatus.Interrupted, transfer.Snapshot()[1].Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => transfer.RunAsync(new()));
        transfer.ResolveReview(false); keyboard.AfterText = null; keyboard.Sent.Clear(); await transfer.RunAsync(new(100,100,100,100,100));
        Assert.Equal("ЦБ-00009123", keyboard.Sent[0]); Assert.DoesNotContain("ЦБ-00008440", keyboard.Sent); Assert.All(transfer.Snapshot(), i=>Assert.Equal(TransferStatus.Completed,i.Status));
    }
    [Fact] public void CrashDuringSendingRestoresAsInterrupted()
    {
        _storage.Write("transfer.json", new[] {new TransferItem(Guid.NewGuid(),"ЦБ-1","Товар","MISC",1,TransferStatus.Sending)});
        var transfer=CreateTransfer(new FakeKeyboard()); transfer.Restore(); Assert.Equal(TransferStatus.Interrupted,transfer.Snapshot()[0].Status); Assert.True(_session.IsFrozen);
    }
    [Fact] public async Task FocusLossMarksCurrentRowFailed()
    {
        _session.ProcessBarcode("001", ScanSource.File); var keyboard=new FakeKeyboard(); var transfer=CreateTransfer(keyboard); transfer.Prepare();
        keyboard.AfterText=_=>keyboard.InvalidTarget=true; await transfer.RunAsync(new(100,100,100,100,100));
        Assert.Equal(TransferStatus.Failed,transfer.Snapshot()[0].Status); Assert.Single(keyboard.Sent);
    }
    [Fact] public void RecoveryBlocksScansAndRestoreKeepsUnknownUnknown()
    {
        _session.ProcessBarcode("999",ScanSource.File); var saved=_session.ReadSaved()!; _catalog.Replace([new("999","NEW","Новый товар","L")]);
        _session.SetRecoveryPending(true); Assert.Throws<InvalidOperationException>(()=>_session.ProcessBarcode("999",ScanSource.Network));
        _session.Restore(saved); Assert.False(_session.Snapshot().Single().Found);
    }
    [Fact] public async Task ShippedExamplesLoadAndMatchExpectedTotals()
    {
        var directory=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../Samples"));
        var loader=new ExcelCatalogLoader(NullLogger<ExcelCatalogLoader>.Instance);
        var catalog=await loader.LoadAsync(Path.Combine(directory,"catalog.xlsx")); Assert.Empty(catalog.Warnings); _catalog.Replace(catalog.Products);
        Assert.NotNull(_catalog.Find("000123"));
        var importer=new TxtImportService(_session,NullLogger<TxtImportService>.Instance); Assert.Equal(6,await importer.ImportAsync(Path.Combine(directory,"scan.txt")));
        Assert.Equal(3,_session.Aggregate().Single(p=>p.Characteristic=="L").Quantity);
    }
    [Fact] public async Task TxtSkipsInvalidLineWithoutLosingFollowingScans()
    {
        var path=Path.Combine(_root,"invalid-scan.txt");await File.WriteAllTextAsync(path,"001\n"+new string('x',257)+"\n002\n");
        var importer=new TxtImportService(_session,NullLogger<TxtImportService>.Instance);Assert.Equal(2,await importer.ImportAsync(path));Assert.Single(importer.Warnings);Assert.Equal(2,_session.Snapshot().Length);
    }
    [Fact] public async Task ExcelDetectsHeadersAndKeepsLeadingZeros()
    {
        var path=Path.Combine(_root,"catalog.xlsx"); using(var book=new XLWorkbook())
        {
            var sheet=book.AddWorksheet("Товары"); sheet.Cell(2,1).Value="Номенклатура"; sheet.Cell(2,2).Value="Характеристика"; sheet.Cell(2,3).Value="Штрихкод"; sheet.Cell(2,4).Value="Артикул";
            sheet.Cell(3,1).Value=" Товар "; sheet.Cell(3,2).Value=" MISC "; sheet.Cell(3,3).Value="000123"; sheet.Cell(3,4).Value=" ЦБ-1 ";
            sheet.Cell(4,1).Value="Товар 2"; sheet.Cell(4,2).Value="XL"; sheet.Cell(4,3).Value=456; sheet.Cell(4,3).Style.NumberFormat.Format="000000"; sheet.Cell(4,4).Value="ЦБ-2";
            sheet.Cell(6,1).Value="Неполная строка"; book.SaveAs(path);
        }
        var result=await new ExcelCatalogLoader(NullLogger<ExcelCatalogLoader>.Instance).LoadAsync(path);
        Assert.Equal(2,result.Products.Count); Assert.Equal("000123",result.Products[0].Barcode); Assert.Equal("000456",result.Products[1].Barcode); Assert.Equal("ЦБ-1",result.Products[0].Article); Assert.Single(result.Warnings);
    }
    [Fact] public async Task ExcelReportsConflictAndMissingHeaders()
    {
        var path=Path.Combine(_root,"invalid.xlsx"); using(var book=new XLWorkbook()) { book.AddWorksheet("Лист").Cell(1,1).Value="Нет заголовков"; book.SaveAs(path); }
        var loader=new ExcelCatalogLoader(NullLogger<ExcelCatalogLoader>.Instance); await Assert.ThrowsAsync<InvalidDataException>(()=>loader.LoadAsync(path));
        using(var book=new XLWorkbook())
        {
            var sheet=book.AddWorksheet("Лист"); string[] headers=["Штрихкод","Артикул","Номенклатура","Характеристика"];
            for(var col=0;col<4;col++)sheet.Cell(1,col+1).Value=headers[col];
            for(var row=2;row<=3;row++){sheet.Cell(row,1).Value="001";sheet.Cell(row,2).Value="ЦБ-"+row;sheet.Cell(row,3).Value="Товар";sheet.Cell(row,4).Value="M";} book.SaveAs(path);
        }
        var error=await Assert.ThrowsAsync<InvalidDataException>(()=>loader.LoadAsync(path)); Assert.Contains("Конфликт",error.Message);
    }
    [Fact] public void InvalidSavedStructureIsRejectedWithoutOverwriting()
    {
        File.WriteAllText(Path.Combine(_root,"session.json"),"{}");Assert.Throws<InvalidDataException>(()=>_session.ReadSaved());Assert.Equal("{}",File.ReadAllText(Path.Combine(_root,"session.json")));
        _storage.Write("transfer.json",new[]{new TransferItem(Guid.NewGuid(),"ЦБ-1","Товар","MISC",0)});
        Assert.Throws<InvalidDataException>(()=>CreateTransfer(new FakeKeyboard()).Restore());
    }
    [Fact] public void SameUserDirectoryRejectsSecondConcurrentWriter()
    {
        Assert.Throws<IOException>(()=>new LocalStorage(_root));
    }
    [Fact] public void PersistenceUsesAtomicReplacementAndRejectsCorruptJson()
    {
        _storage.Write("value.json",new[]{1,2}); _storage.Write("value.json",new[]{3}); Assert.Equal(new[]{3},_storage.Read<int[]>("value.json")); Assert.False(File.Exists(Path.Combine(_root,"value.json.tmp")));
        File.WriteAllText(Path.Combine(_root,"value.json"),"broken"); Assert.Throws<System.Text.Json.JsonException>(()=>_storage.Read<int[]>("value.json"));
    }
    private TransferQueueService CreateTransfer(FakeKeyboard keyboard)=>new(_session,_storage,keyboard,NullLogger<TransferQueueService>.Instance);
    private sealed class FakeKeyboard : IOneCKeyboard
    {
        public bool IsSupported=>true;
        public List<string> Sent {get;}=[];
        public Action<string>? AfterText {get;set;}
        public bool InvalidTarget {get;set;}
        public void CaptureTarget()=>ValidateTarget();
        public void ValidateTarget(){if(InvalidTarget)throw new InvalidOperationException("Окно сменилось");}
        public void TypeText(string text,CancellationToken token){token.ThrowIfCancellationRequested();Sent.Add(text);AfterText?.Invoke(text);}
        public void Enter(CancellationToken token){token.ThrowIfCancellationRequested();Sent.Add("ENTER");}
    }
}
