using FanShop.PriceTags.Models;
using Microsoft.Extensions.Logging;

namespace FanShop.PriceTags.Services;

public interface IOneCKeyboard
{
    bool IsSupported { get; }
    void CaptureTarget();
    void ValidateTarget();
    void TypeText(string text, CancellationToken token);
    void Enter(CancellationToken token);
}
public sealed class TransferQueueService(ScanSessionService session, LocalStorage storage, IOneCKeyboard keyboard, ILogger<TransferQueueService> logger)
{
    private readonly object _gate = new();
    private TransferItem[] _items = [];
    private CancellationTokenSource? _cancel;
    private volatile bool _pause;
    private bool _running;
    public event Action? Changed;
    public string Message { get; private set; } = "Очередь не сформирована.";
    public bool Running { get { lock (_gate) return _running; } }
    public bool IsSupported => keyboard.IsSupported;
    public TransferItem[] Snapshot() { lock (_gate) return _items.ToArray(); }
    public void Restore()
    {
        var saved = storage.Read<TransferItem[]>("transfer.json");
        if (saved is not { Length: > 0 }) return;
        if (saved.Any(i => i is null || i.Id == Guid.Empty || i.Quantity <= 0 || !Enum.IsDefined(i.Status)
                || new[] { i.Article, i.Name, i.Characteristic }.Any(s => string.IsNullOrWhiteSpace(s) || s.Any(char.IsControl)))
            || saved.Select(i => i.Id).Distinct().Count() != saved.Length)
            throw new InvalidDataException("Файл transfer.json имеет некорректную структуру. Сохраните резервную копию и исправьте файл при закрытом FanShop.");
        lock (_gate)
        {
            _items = saved.Select(i => i.Status == TransferStatus.Sending ? i with { Status = TransferStatus.Interrupted } : i).ToArray();
            Persist(); Message = "Восстановлена очередь. Проверьте строку 1С, если передача была прервана.";
        }
        session.SetFrozen(true); Changed?.Invoke();
    }
    public void Prepare()
    {
        lock (_gate)
        {
            if (_running || _items.Length > 0) throw new InvalidOperationException("Уже существует очередь. Продолжите её или закройте.");
            if (!keyboard.IsSupported) throw new PlatformNotSupportedException("Передача в 1С доступна только в Windows.");
            var aggregates = session.FreezeForTransfer();
            try
            {
                _items = aggregates.Select(p => new TransferItem(Guid.NewGuid(), p.Article, p.Name, p.Characteristic, p.Quantity)).ToArray();
                Persist(); Message = "Очередь готова. Перейдите в первую ячейку 1С и нажмите F8.";
            }
            catch { _items = []; session.SetFrozen(false); throw; }
        }
        Changed?.Invoke();
    }
    public void Pause() { _pause = true; Message = "Пауза: текущая строка будет завершена. F8 — продолжить, Esc — остановить немедленно."; Changed?.Invoke(); }
    public void Stop()
    {
        lock (_gate) { _cancel?.Cancel(); Message = "Остановка передачи…"; }
        Changed?.Invoke();
    }
    public void ResolveReview(bool alreadyCompleted)
    {
        lock (_gate)
        {
            if (_running) throw new InvalidOperationException("Сначала остановите передачу.");
            var index = Array.FindIndex(_items, i => i.Status is TransferStatus.Interrupted or TransferStatus.Failed);
            if (index < 0) throw new InvalidOperationException("Нет позиции, требующей проверки.");
            SetItem(index, _items[index] with { Status = alreadyCompleted ? TransferStatus.Completed : TransferStatus.Pending });
            Message = alreadyCompleted ? "Строка подтверждена как переданная. Поставьте курсор в следующую пустую строку и нажмите F8." : "Повтор разрешён. Удалите частичную строку 1С и подготовьте пустую строку перед F8.";
        }
        Changed?.Invoke();
    }
    public void CloseQueue()
    {
        lock (_gate)
        {
            if (_running) throw new InvalidOperationException("Сначала остановите передачу.");
            storage.Write("transfer.json", Array.Empty<TransferItem>()); _items = [];
            Message = "Очередь закрыта. Сканирования сохранены; для новой партии очистите сессию.";
        }
        session.SetFrozen(false); Changed?.Invoke();
    }
    public async Task RunAsync(TransferDelays delays)
    {
        delays.Validate();
        CancellationToken token;
        lock (_gate)
        {
            if (_running) { _pause = false; return; }
            if (_items.Length == 0) throw new InvalidOperationException("Сначала сформируйте очередь кнопкой «Передать в 1С».");
            if (_items.Any(i => i.Status is TransferStatus.Interrupted or TransferStatus.Failed)) throw new InvalidOperationException("Сначала проверьте частичную строку 1С и выберите результат проверки в FanShop.");
            if (_items.All(i => i.Status == TransferStatus.Completed)) throw new InvalidOperationException("Все позиции уже переданы. Закройте очередь.");
            keyboard.CaptureTarget();
            _cancel?.Dispose(); _cancel = new(); token = _cancel.Token; _running = true; _pause = false;
            Message = "Передача выполняется. F9 — пауза после строки; Esc — немедленная остановка.";
        }
        Changed?.Invoke(); logger.LogInformation("Начало передачи в 1С");
        await Task.Run(async () =>
        {
            var current = -1;
            try
            {
                for (var index = 0; index < _items.Length; index++)
                {
                    if (_items[index].Status == TransferStatus.Completed) continue;
                    while (_pause) { token.ThrowIfCancellationRequested(); await Task.Delay(25, token); }
                    token.ThrowIfCancellationRequested(); keyboard.ValidateTarget();
                    current = index;
                    lock (_gate) { SetItem(index, _items[index] with { Status = TransferStatus.Sending }); }
                    Changed?.Invoke();
                    var item = _items[index];
                    keyboard.TypeText(item.Article, token); await Delay(delays.ArticleText, token);
                    keyboard.Enter(token); await Delay(delays.ArticleEnter, token);
                    keyboard.TypeText(item.Characteristic, token); await Delay(delays.CharacteristicText, token);
                    keyboard.Enter(token); await Delay(delays.CharacteristicEnter, token);
                    keyboard.TypeText(item.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture), token);
                    await Delay(150, token); keyboard.Enter(token); await Delay(delays.QuantityEnter, token);
                    lock (_gate) { SetItem(index, _items[index] with { Status = TransferStatus.Completed }); }
                    current = -1; Changed?.Invoke();
                }
                Message = "Все позиции переданы. Проверьте результат в 1С. Очередь можно закрыть.";
            }
            catch (Exception ex)
            {
                lock (_gate)
                {
                    if (current >= 0)
                    {
                        try { SetItem(current, _items[current] with { Status = ex is OperationCanceledException ? TransferStatus.Interrupted : TransferStatus.Failed }); }
                        catch (Exception saveError) { _items[current] = _items[current] with { Status = TransferStatus.Failed }; logger.LogError(saveError, "Не удалось сохранить остановку; сохранённый Sending будет требовать проверки при восстановлении"); }
                    }
                    Message = current >= 0 ? $"Передача остановлена на позиции {current + 1}. Проверьте текущую строку в 1С перед продолжением. {ex.Message}" : $"Передача остановлена между строками. {ex.Message}";
                }
                logger.LogWarning(ex, "Передача остановлена, позиция {Index}", current + 1);
            }
            finally { lock (_gate) _running = false; logger.LogInformation("Передача завершена/остановлена"); Changed?.Invoke(); }
        });
    }
    private async Task Delay(int milliseconds, CancellationToken token)
    {
        var left = milliseconds;
        while (left > 0) { token.ThrowIfCancellationRequested(); keyboard.ValidateTarget(); var step = Math.Min(25, left); await Task.Delay(step, token); left -= step; }
    }
    private void SetItem(int index, TransferItem item)
    {
        var next = _items.ToArray(); next[index] = item; storage.Write("transfer.json", next); _items = next;
    }
    private void Persist() => storage.Write("transfer.json", _items);
}
