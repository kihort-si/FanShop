using System.Text.Json;

namespace FanShop.Replenishment;

public sealed class ReplenishmentStore : IDisposable
{
    private readonly string _root;
    private readonly FileStream _lease;
    private readonly object _gate = new();
    private bool _disposed;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public ReplenishmentStore(string root)
    {
        _root = root; Directory.CreateDirectory(root);
        try { _lease = new FileStream(Path.Combine(root, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new IOException("Учёт довесов уже открыт в другом экземпляре FanShop. Закройте второй экземпляр приложения.", ex); }
    }
    private string DayPath(DateOnly day) => Path.Combine(_root, $"day-{day:yyyy-MM-dd}.json");
    private T? Read<T>(string path) => File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json)
        ?? throw new InvalidDataException("Файл учёта пуст или повреждён: " + path) : default;
    private static void Write<T>(string path, T value)
    {
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, value, Json); stream.Flush(true); }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public ReplenishmentDay Load(DateOnly day)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var state = Read<ReplenishmentDay>(DayPath(day)) ?? new() { Day = day };
            if (state.Format is not (1 or 2) || state.Day != day || state.Items is null || state.Events is null || state.Reports is null || state.LabelBatches is null)
                throw new InvalidDataException("Неизвестный формат файла довесов. Данные сохранены без изменений.");
            if (state.Format == 1)
            {
                var backup = DayPath(day) + ".v1-backup";
                if (!File.Exists(backup)) File.Copy(DayPath(day), backup);
                state.Items = InventoryAggregation.Merge(state.Items).ToList();
                state.Format = 2;
            }
            return state;
        }
    }
    public ExclusionRule[] Rules()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Read<ExclusionRule[]>(Path.Combine(_root, "exclusions.json")) ?? [];
        }
    }
    public void AddRule(ExclusionKind kind, string value)
    {
        lock (_gate)
        {
            value = value.Trim();
            if (value.Length == 0) throw new InvalidOperationException("Введите артикул или слова из названия.");
            var rules = Rules();
            if (rules.Any(r => r.Kind == kind && InventoryKeys.Normalize(r.Value) == InventoryKeys.Normalize(value))) return;
            Write(Path.Combine(_root, "exclusions.json"), rules.Append(new ExclusionRule(Guid.NewGuid(), kind, value)).ToArray());
        }
    }
    public void RemoveRule(Guid id)
    {
        lock (_gate) Write(Path.Combine(_root, "exclusions.json"), Rules().Where(r => r.Id != id).ToArray());
    }
    public string Import(InventoryReport report)
    {
        lock (_gate)
        {
            if (report.Items.Length == 0 || report.Items.Select(i => i.Key).Distinct().Count() != report.Items.Length)
                throw new InvalidDataException("Отчёт пуст или содержит повторяющиеся товары.");
            var state = Load(report.Day);
            if (state.Reports.Any(r => r.Hash == report.Hash)) return "Этот файл уже загружен. Данные и отметки не изменены.";
            const string mode = "Накопленный за день";
            var before = state.Items.ToDictionary(i => i.Key);
            var current = before.ToDictionary(p => p.Key, p => p.Value);
            var incomingKeys = report.Items.Select(i => i.Key).ToHashSet();
            var missing = before.Values.Count(i => i.Expense >= 1 && !incomingKeys.Contains(i.Key));
            // A filtered export can omit a sold-out product: keep its known expense.
            var decreases = 0;
            foreach (var item in report.Items)
            {
                if (before.TryGetValue(item.Key, out var previous))
                {
                    if (item.Expense < previous.Expense) decreases++;
                    current[item.Key] = item;
                }
                else current[item.Key] = item;
            }
            state.Items = current.Values.ToList();
            state.Reports.Add(new(Guid.NewGuid(), DateTimeOffset.Now, report.FileName, report.Hash,
                report.Items.Length, report.Items.Sum(i => i.Expense), mode,
                report.Items.Select(i => i.Warehouse).Distinct().ToArray()));
            Write(DayPath(report.Day), state);
            return $"Загружено строк: {report.Items.Length}. Отчёт за {report.Day:dd.MM.yyyy}. Отметки довесов сохранены."
                + (missing > 0 ? $" В новой выгрузке отсутствуют {missing} ранее проданных позиций; их расход и отметки сохранены." : "")
                + (decreases > 0 ? $" Внимание: расход уменьшился у {decreases} позиций. Проверьте период выгрузки: возможно, выбран более ранний отчёт или были возвраты." : "");
        }
    }
    public ReplenishmentRow[] Rows(DateOnly day)
    {
        lock (_gate)
        {
            var state = Load(day); var rules = Rules();
            var completed = state.Events.GroupBy(e => e.Item.Key).ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity));
            var active = state.LabelBatches.Where(b => b.Cancelled is null).ToArray();
            decimal LabelCount(string key, bool printed) => active.Where(b => (b.Printed is not null) == printed)
                .SelectMany(b => b.Lines).Where(l => l.Key == key).Sum(l => (decimal)l.Quantity);
            return state.Items.Where(i => i.Expense >= 1 || completed.GetValueOrDefault(i.Key) > 0 || LabelCount(i.Key, true) > 0 || LabelCount(i.Key, false) > 0)
                .Select(i => new ReplenishmentRow(i, completed.GetValueOrDefault(i.Key),
                    string.Join("; ", rules.Where(r => r.Matches(i)).Select(r => r.KindName + ": " + r.Value)),
                    LabelCount(i.Key, true), LabelCount(i.Key, false)))
                .OrderBy(r => r.Name).ThenBy(r => r.Characteristic).ToArray();
        }
    }
    public void Complete(DateOnly day, string key, decimal quantity)
    {
        lock (_gate)
        {
            var row = Rows(day).SingleOrDefault(r => r.Key == key) ?? throw new InvalidOperationException("Позиция не найдена.");
            if (quantity <= 0 || quantity > row.Remaining) throw new InvalidOperationException($"Можно отметить от 0 до {row.Remaining} шт. (больше нуля).");
            var state = Load(day);
            state.Events.Add(new(Guid.NewGuid(), DateTimeOffset.Now, row.Item, quantity));
            Write(DayPath(day), state);
        }
    }
    public void Undo(DateOnly day, Guid eventId)
    {
        lock (_gate)
        {
            var state = Load(day);
            var entry = state.Events.SingleOrDefault(e => e.Id == eventId) ?? throw new InvalidOperationException("Отметка не найдена.");
            if (entry.UndoOf is not null || state.Events.Any(e => e.UndoOf == eventId))
                throw new InvalidOperationException("Эта отметка уже отменена или сама является отменой.");
            state.Events.Add(new(Guid.NewGuid(), DateTimeOffset.Now, entry.Item, -entry.Quantity, entry.Id));
            Write(DayPath(day), state);
        }
    }
    public ReplenishmentLabelBatch ReserveLabels(DateOnly day, IReadOnlyDictionary<string, int> quantities)
    {
        lock (_gate)
        {
            if (quantities.Count == 0) throw new InvalidOperationException("Выберите товары и количество ценников.");
            var rows = Rows(day).ToDictionary(r => r.Key);
            var lines = quantities.Select(q =>
            {
                if (!rows.TryGetValue(q.Key, out var row) || q.Value <= 0 || q.Value > row.LabelsAvailable)
                    throw new InvalidOperationException("Количество превышает довешенное за вычетом уже напечатанных и зарезервированных ценников. Обновите выбор.");
                if (new[] { row.Article, row.Name, row.Characteristic }.Any(v => string.IsNullOrWhiteSpace(v) || v.Any(char.IsControl)))
                    throw new InvalidOperationException("Для передачи нужны артикул, наименование и характеристика без управляющих символов.");
                return new ReplenishmentLabelLine(Guid.NewGuid(), q.Key, row.Article, row.Name, row.Characteristic, q.Value);
            }).ToArray();
            var state = Load(day);
            var batch = new ReplenishmentLabelBatch(Guid.NewGuid(), DateTimeOffset.Now, lines);
            state.LabelBatches.Add(batch); Write(DayPath(day), state); return batch;
        }
    }
    public void MarkLabelsSent(DateOnly day, Guid batchId) => ChangeBatch(day, batchId, b => b with { Sent = b.Sent ?? DateTimeOffset.Now });
    public void ConfirmLabelsPrinted(DateOnly day, Guid batchId) => ChangeBatch(day, batchId, b => b with { Printed = DateTimeOffset.Now });
    public void CancelLabelBatch(DateOnly day, Guid batchId) => ChangeBatch(day, batchId, b =>
        b.Printed is not null ? throw new InvalidOperationException("Печать этой партии уже подтверждена.") : b with { Cancelled = DateTimeOffset.Now });
    private void ChangeBatch(DateOnly day, Guid id, Func<ReplenishmentLabelBatch, ReplenishmentLabelBatch> change)
    {
        lock (_gate)
        {
            var state = Load(day); var index = state.LabelBatches.FindIndex(b => b.Id == id);
            if (index < 0) throw new InvalidOperationException("Партия не найдена.");
            var batch = state.LabelBatches[index];
            if (batch.Cancelled is not null || batch.Printed is not null) throw new InvalidOperationException("Партия уже закрыта.");
            state.LabelBatches[index] = change(batch); Write(DayPath(day), state);
        }
    }
    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; _lease.Dispose(); }
    }
}
