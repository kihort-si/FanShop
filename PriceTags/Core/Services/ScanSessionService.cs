using FanShop.PriceTags.Models;
using Microsoft.Extensions.Logging;

namespace FanShop.PriceTags.Services;

public sealed record PersistedSession(ScanRecord[] Records, ScanRecord[] Receipts);

public sealed class ScanSessionService(ProductCatalogService catalog, LocalStorage storage, ILogger<ScanSessionService> logger)
{
    private readonly object _gate = new();
    private List<ScanRecord> _records = [];
    private readonly Dictionary<Guid, ScanRecord> _receipts = []; // retain acknowledgements even after Undo
    private ScanRecord[] _savedReceipts = [];
    private bool _frozen;
    private bool _recoveryPending;
    public event Action? Changed;
    public bool IsFrozen { get { lock (_gate) return _frozen || _recoveryPending; } }
    public ScanRecord[] Snapshot() { lock (_gate) return _records.ToArray(); }
    public ScanRecord[]? ReadSaved()
    {
        var saved = storage.Read<PersistedSession>("session.json");
        if (saved is not null && (saved.Records is null || saved.Receipts is null
            || saved.Records.Concat(saved.Receipts).Any(r => r is null || r.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(r.Barcode) || r.Barcode.Length > 256 || r.Barcode.Any(char.IsControl)
                || !Enum.IsDefined(r.Source) || (r.Product is { } p && (p.Barcode != r.Barcode
                    || new[] { p.Article, p.Name, p.Characteristic }.Any(s => string.IsNullOrWhiteSpace(s) || s.Any(char.IsControl)))))))
            throw new InvalidDataException("Файл session.json имеет некорректную структуру. Сохраните резервную копию и исправьте файл при закрытом FanShop.");
        lock (_gate)
        {
            _savedReceipts = saved?.Receipts ?? [];
            foreach (var receipt in _savedReceipts) _receipts[receipt.Id] = receipt;
        }
        return saved?.Records;
    }
    public void SetRecoveryPending(bool value) { lock (_gate) _recoveryPending = value; }
    public void Restore(IEnumerable<ScanRecord> records)
    {
        lock (_gate)
        {
            var next = records.ToList();
            foreach (var receipt in _savedReceipts) _receipts[receipt.Id] = receipt;
            Save(next); _records = next;
            foreach (var r in next) _receipts[r.Id] = r;
            _recoveryPending = false;
        }
        Changed?.Invoke();
    }
    public void DiscardSaved()
    {
        lock (_gate) { foreach (var receipt in _savedReceipts) _receipts[receipt.Id] = receipt; Save([]); _recoveryPending = false; }
    }
    public ScanRecord ProcessBarcode(string barcode, ScanSource source, Guid? requestId = null)
    {
        var clean = barcode.Trim();
        if (clean.Length is 0 or > 256 || clean.Any(char.IsControl)) throw new InvalidDataException("Штрихкод должен содержать от 1 до 256 символов без управляющих символов.");
        ScanRecord record;
        lock (_gate)
        {
            if (requestId is { } id && _receipts.TryGetValue(id, out var existing))
            {
                if (existing.Barcode != clean) throw new InvalidDataException("Идентификатор запроса уже использован для другого штрихкода.");
                return existing;
            }
            EnsureEditable();
            if (catalog.Count == 0) throw new InvalidOperationException("Сначала загрузите Excel-справочник на ноутбуке.");
            record = new(requestId ?? Guid.NewGuid(), clean, DateTimeOffset.Now, source, catalog.Find(clean));
            var next = new List<ScanRecord>(_records) { record };
            Save(next, record); _records = next; _receipts[record.Id] = record;
        }
        if (!record.Found) logger.LogWarning("Неизвестный штрихкод {Barcode}", clean);
        Changed?.Invoke();
        return record;
    }
    public bool Undo()
    {
        lock (_gate)
        {
            EnsureEditable();
            if (_records.Count == 0) return false;
            var next = _records.Take(_records.Count - 1).ToList(); Save(next); _records = next;
        }
        Changed?.Invoke(); return true;
    }
    public void Remove(Guid id)
    {
        lock (_gate) { EnsureEditable(); var next = _records.Where(r => r.Id != id).ToList(); Save(next); _records = next; }
        Changed?.Invoke();
    }
    public void Clear()
    {
        lock (_gate) { EnsureEditable(); Save([]); _records = []; }
        Changed?.Invoke();
    }
    public void ReplaceCatalog(IEnumerable<ProductInfo> products)
    {
        lock (_gate) { EnsureEditable(); catalog.Replace(products); }
        Changed?.Invoke(); // existing records keep the lookup result at scan time
    }
    public AggregatedProduct[] Aggregate() => Aggregate(Snapshot());
    public static AggregatedProduct[] Aggregate(IEnumerable<ScanRecord> records) => records.Where(r => r.Product is not null)
        .GroupBy(r => (r.Product!.Article, r.Product.Characteristic, r.Product.Name))
        .Select(g => new AggregatedProduct(g.Key.Article, g.Key.Name, g.Key.Characteristic, g.Count()))
        .OrderBy(p => p.Article, StringComparer.Ordinal).ThenBy(p => p.Characteristic, StringComparer.Ordinal).ToArray();
    public AggregatedProduct[] FreezeForTransfer()
    {
        lock (_gate)
        {
            EnsureEditable();
            if (catalog.Count == 0) throw new InvalidOperationException("Не загружен справочник.");
            if (_records.Count == 0) throw new InvalidOperationException("Сессия пуста.");
            if (_records.Any(r => !r.Found)) throw new InvalidOperationException("Удалите неизвестные штрихкоды из истории перед передачей.");
            var items = Aggregate(_records);
            if (items.GroupBy(i => (i.Article, i.Characteristic)).Any(g => g.Select(i => i.Name).Distinct().Count() > 1))
                throw new InvalidOperationException("Один артикул и характеристика имеют разные названия. Очистите сессию и загрузите согласованный справочник.");
            _frozen = true; return items;
        }
    }
    public void SetFrozen(bool frozen) { lock (_gate) _frozen = frozen; Changed?.Invoke(); }
    private void EnsureEditable()
    {
        if (_frozen || _recoveryPending) throw new InvalidOperationException("Сессия заблокирована: завершите восстановление или закройте очередь передачи.");
    }
    private void Save(List<ScanRecord> records, ScanRecord? newReceipt = null)
    {
        var receipts = _receipts.Values.ToList();
        if (newReceipt is not null) receipts.Add(newReceipt);
        storage.Write("session.json", new PersistedSession(records.ToArray(), receipts.ToArray()));
    }
}
public sealed class TxtImportService(ScanSessionService session, ILogger<TxtImportService> logger)
{
    public IReadOnlyList<string> Warnings { get; private set; } = [];
    public async Task<int> ImportAsync(string path, CancellationToken token = default)
    {
        return await Task.Run(async () =>
        {
            var count = 0; var lineNumber = 0; var warnings = new List<string>();
            Warnings = warnings;
            using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
            while (await reader.ReadLineAsync(token) is { } line)
            {
                token.ThrowIfCancellationRequested(); lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                try { session.ProcessBarcode(line, ScanSource.File); count++; }
                catch (InvalidDataException ex) { warnings.Add($"Строка {lineNumber} пропущена: {ex.Message}"); logger.LogWarning("TXT {Path}: строка {Line} пропущена", path, lineNumber); }
            }
            logger.LogInformation("TXT {Path}: добавлено {Count} сканирований", path, count);
            return count;
        }, token);
    }
}
